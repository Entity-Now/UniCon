using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core.Models;

namespace UniCon.Core.Notification;

/// <summary>
/// 基于 Channel&lt;T&gt; 的异步通知分发器。
/// 采集线程只负责 TryWrite；独立消费线程负责 await callback，
/// 彻底解耦扫描循环与用户回调，防止 callback 阻塞扫描线程。
/// </summary>
internal sealed class NotificationDispatcher : IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly Channel<NotificationEnvelope> _channel;
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    /// <param name="boundedCapacity">队列最大容量；满时丢弃最旧数据（Backpressure 控制）</param>
    public NotificationDispatcher(ILogger logger, int boundedCapacity = 4096)
    {
        _logger  = logger;
        _channel = Channel.CreateBounded<NotificationEnvelope>(new BoundedChannelOptions(boundedCapacity)
        {
            FullMode      = BoundedChannelFullMode.DropOldest,  // 满时丢弃最旧数据
            SingleReader  = true,
            SingleWriter  = false,
            AllowSynchronousContinuations = false
        });
    }

    /// <summary>生产端：非阻塞投递，失败静默（由调用方记录统计）</summary>
    public bool TryEnqueue(NotificationEnvelope envelope)
        => _channel.Writer.TryWrite(envelope);

    public void Start(CancellationToken externalCt)
    {
        _cts          = CancellationTokenSource.CreateLinkedTokenSource(externalCt);
        _consumerTask = Task.Run(() => ConsumeLoopAsync(_cts.Token), _cts.Token);
    }

    private async Task ConsumeLoopAsync(CancellationToken ct)
    {
        await foreach (var envelope in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await envelope.Callback(envelope.Value).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 异常隔离：单个 callback 失败不影响后续通知消费
                _logger.LogDebug(ex,
                    "Callback exception isolated: subscriptionId={Id} address={Address}",
                    envelope.SubscriptionId, envelope.Address);
            }
        }
    }

    public async Task StopAsync()
    {
        _channel.Writer.TryComplete();
        _cts?.Cancel();

        if (_consumerTask is not null)
        {
            try
            {
                await _consumerTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 正常取消路径：CTS 取消后 ReadAllAsync 抛出 OperationCanceledException，属预期行为
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
