using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Notification;

namespace UniCon.Core.Scanning;

/// <summary>
/// 基于"最小下次执行时间"调度的扫描器。
/// 不使用 while(true)+Delay(50) 全量扫描，而是动态计算最近到期 ScanGroup 的等待时间。
/// Scheduler 不感知 Callback，只负责采集 → 投递 NotificationEnvelope，
/// 实际 Callback 执行由 NotificationDispatcher 独立线程消费。
/// </summary>
internal sealed class ScanScheduler : IAsyncDisposable
{
    private readonly string _driverId;
    private readonly ILogger _logger;
    private readonly ScanGroupRegistry _registry;
    private readonly IUniconCacheProvider _cacheProvider;
    private readonly NotificationDispatcher _dispatcher;
    private readonly Func<IEnumerable<UniconRequest>, CancellationToken, Task<IEnumerable<UniconResponse<object>>>> _batchReader;

    private CancellationTokenSource? _cts;
    private Task? _schedulerTask;

    public ScanScheduler(
        string driverId,
        ILogger logger,
        ScanGroupRegistry registry,
        IUniconCacheProvider cacheProvider,
        NotificationDispatcher dispatcher,
        Func<IEnumerable<UniconRequest>, CancellationToken, Task<IEnumerable<UniconResponse<object>>>> batchReader)
    {
        _driverId    = driverId;
        _logger      = logger;
        _registry    = registry;
        _cacheProvider = cacheProvider;
        _dispatcher  = dispatcher;
        _batchReader = batchReader;
    }

    public void Start(CancellationToken externalCt)
    {
        _cts          = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _schedulerTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        _dispatcher.Start(_cts.Token);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var now      = DateTime.UtcNow;
                var minWaitMs = int.MaxValue;

                foreach (var group in _registry.GetAll())
                {
                    if (group.IsDue(now))
                    {
                        await ScanGroupAsync(group, now, ct);
                        // 下次等待至少一个 ScanRate 后
                        if (group.ScanRateMs < minWaitMs)
                            minWaitMs = group.ScanRateMs;
                    }
                    else
                    {
                        var wait = group.MillisecondsUntilDue(now);
                        if (wait < minWaitMs)
                            minWaitMs = wait;
                    }
                }

                // 无活跃组时以 100ms 兜底等待，避免 CPU 空转
                var delayMs = minWaitMs == int.MaxValue ? 100 : Math.Clamp(minWaitMs, 1, 5000);
                await Task.Delay(delayMs, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Driver:{Id}] ScanScheduler unhandled error", _driverId);
                await Task.Delay(500, ct);
            }
        }
    }

    private async Task ScanGroupAsync(ScanGroup group, DateTime now, CancellationToken ct)
    {
        var tags = group.GetTags();
        if (tags.Count == 0)
        {
            group.LastPolledTime = now;
            return;
        }

        // 构建请求列表，避免 LINQ ToList
        var requests = new List<UniconRequest>(tags.Count);
        foreach (var tag in tags)
            requests.Add(new UniconRequest { Address = tag.Address });

        var sw = Stopwatch.StartNew();
        try
        {
            var results = await _batchReader(requests, ct);
            sw.Stop();
            group.Statistics.RecordScan(sw.ElapsedMilliseconds);
            group.LastPolledTime = now;

            DispatchResults(group, tags, requests, results, now);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            group.Statistics.RecordError();
            group.LastPolledTime = now;
            _logger.LogDebug(ex,
                "[Driver:{Id}] Batch read error for ScanGroup {Rate}ms", _driverId, group.ScanRateMs);
        }
    }

    private void DispatchResults(
        ScanGroup group,
        ICollection<TagEntry> tags,
        List<UniconRequest> requests,
        IEnumerable<UniconResponse<object>> results,
        DateTime now)
    {
        // 构建 address → response 映射，避免嵌套遍历
        var resultMap = new Dictionary<string, UniconResponse<object>>(
            requests.Count, StringComparer.OrdinalIgnoreCase);

        var idx = 0;
        foreach (var r in results)
        {
            if (idx >= requests.Count) break;
            resultMap[requests[idx].Address] = r;
            idx++;
        }

        foreach (var tag in tags)
        {
            if (!resultMap.TryGetValue(tag.Address, out var response)) continue;
            if (!response.Success || response.Data is null) continue;

            var newValue = response.Data;
            newValue.ServerTimestamp = now;

            if (!group.Strategy.ShouldNotify(newValue, tag.LastValue, tag.Metadata))
                continue;

            tag.UpdateCache(newValue);

            // 同步更新缓存（fire-and-forget；不阻塞扫描线程）
            _ = _cacheProvider.SetAsync(_driverId, tag.Address, newValue);

            foreach (var kv in tag.GetSubscribers())
            {
                var envelope = new NotificationEnvelope(kv.Key, tag.Address, newValue, kv.Value);

                if (_dispatcher.TryEnqueue(envelope))
                    group.Statistics.RecordNotify();
                else
                {
                    group.Statistics.RecordError();
                    _logger.LogDebug(
                        "[Driver:{Id}] Notification queue full, dropped for '{Addr}'",
                        _driverId, tag.Address);
                }
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        await _dispatcher.StopAsync();

        if (_schedulerTask is not null)
        {
            try
            {
                await _schedulerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消路径
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
