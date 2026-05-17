using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Notification;
using UniCon.Core.Scanning;

namespace UniCon.Core;

/// <summary>
/// 驱动抽象基类 v2 — 职责拆分后的精简协调层。
///
/// 职责边界：
///   • ConnectionManager  — ConnectAsync / DisconnectAsync / Watchdog
///   • ScanGroupRegistry  — 订阅注册时完成 Address 去重与分组
///   • ScanScheduler      — 基于最小等待时间调度到期 ScanGroup
///   • NotificationDispatcher — Channel 解耦采集线程与 Callback
///   • IUniconCacheProvider — DI 注入，非 static
///
/// 不在 DriverBase 中：
///   • 批量读取的具体实现（由子类 override ReadBatchAsync）
///   • Callback 的直接 await（委托给 NotificationDispatcher）
///   • LastPolledTime 存储（属于 ScanGroup）
///   • Finalizer（已移除，使用标准 IDisposable）
/// </summary>
public abstract class DriverBase : IUniconDriver
{
    // ─── 依赖 ──────────────────────────────────────────────────────────────
    protected readonly ILogger _logger;
    private   readonly IUniconCacheProvider _cacheProvider;

    // ─── 连接状态（无锁读取）─────────────────────────────────────────────
    private          string? _connectionString;
    private int _state = (int)DriverState.Disconnected;
    private          int     _reconnectAttempt;

    // ─── 连接串行化锁（仅连接操作，非读写路径）─────────────────────────
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // ─── Watchdog ─────────────────────────────────────────────────────────
    private CancellationTokenSource? _watchdogCts;

    // ─── 订阅索引（subscriptionId → 路由信息，用于精准取消订阅）────────
    private readonly ConcurrentDictionary<string, SubscriptionRoute> _subscriptionIndex = new();

    // ─── 扫描基础设施 ─────────────────────────────────────────────────────
    private readonly ScanGroupRegistry    _registry   = new();
    private          ScanScheduler?       _scheduler;
    private          NotificationDispatcher? _dispatcher;
    private          CancellationTokenSource? _schedulerCts;

    // ─── 公开属性 ─────────────────────────────────────────────────────────
    public string  DriverId         { get; }
    public string? ConnectionString => _connectionString;

    public DriverState State
    {
        get => (DriverState)Volatile.Read(ref _state);
        protected set
        {
            var old = (DriverState)Interlocked.Exchange(ref _state, (int)value);
            if (old != value)
                StateChanged?.Invoke(this, new DriverStateChangedEventArgs(DriverId, old, value));
        }
    }

    public bool IsConnected => State == DriverState.Connected;

    public event EventHandler<DriverStateChangedEventArgs>? StateChanged;

    // ─── 重连 & 扫描配置 ──────────────────────────────────────────────────
    public bool EnableAutoReconnect  { get; set; } = true;
    public int  MaxRetryIntervalMs   { get; set; } = 30_000;
    public int  InitialRetryIntervalMs { get; set; } = 1_000;
    public UniconScanMode DefaultScanMode { get; set; } = UniconScanMode.ExceptionBased;
    public int  DefaultScanRateMs    { get; set; } = 1_000;

    // ─── 构造函数（CacheProvider 通过 DI 注入，非 static）───────────────
    protected DriverBase(string driverId, ILogger logger, IUniconCacheProvider cacheProvider)
    {
        DriverId       = driverId;
        _logger        = logger;
        _cacheProvider = cacheProvider;
    }

    // =========================================================================
    // 连接管理
    // =========================================================================

    public async Task<bool> ConnectAsync(string connectionString, CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct);
        try
        {
            _connectionString = connectionString;
            var success = await PerformConnectAsync(connectionString, ct);

            if (success && EnableAutoReconnect && _watchdogCts is null)
                StartWatchdog();

            return success;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task<bool> PerformConnectAsync(string connectionString, CancellationToken ct)
    {
        if (State == DriverState.Connected) return true;

        State = DriverState.Connecting;
        _logger.LogInformation("[Driver:{Id}] Connecting...", DriverId);

        try
        {
            var success = await OnConnectAsync(connectionString, ct);
            if (success)
            {
                State = DriverState.Connected;
                _reconnectAttempt = 0;
                _logger.LogInformation("[Driver:{Id}] Connected.", DriverId);
                EnsureSchedulerStarted();
            }
            else
            {
                State = DriverState.Faulted;
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Driver:{Id}] Connection failed.", DriverId);
            State = DriverState.Faulted;
            return false;
        }
    }

    protected abstract Task<bool> OnConnectAsync(string connectionString, CancellationToken ct);

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        StopWatchdog();
        await StopSchedulerAsync();

        await _connectionLock.WaitAsync(ct);
        try
        {
            State = DriverState.Disconnecting;
            await OnDisconnectAsync(ct);
            State = DriverState.Disconnected;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    protected abstract Task OnDisconnectAsync(CancellationToken ct);

    // =========================================================================
    // Watchdog（与 Scheduler 解耦，仅负责心跳检测与重连触发）
    // =========================================================================

    private void StartWatchdog()
    {
        _watchdogCts = new CancellationTokenSource();
        Task.Run(() => WatchdogLoopAsync(_watchdogCts.Token));
        _logger.LogDebug("[Driver:{Id}] Watchdog started.", DriverId);
    }

    private void StopWatchdog()
    {
        _watchdogCts?.Cancel();
        _watchdogCts?.Dispose();
        _watchdogCts = null;
    }

    private async Task WatchdogLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (State is DriverState.Faulted or DriverState.Disconnected)
                    await ReconnectAsync(ct);
                else if (State == DriverState.Connected && !await PingAsync(ct))
                {
                    _logger.LogWarning("[Driver:{Id}] Heartbeat lost. Marking Faulted.", DriverId);
                    State = DriverState.Faulted;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Driver:{Id}] Watchdog error.", DriverId);
            }

            await Task.Delay(CalculateRetryDelayMs(), ct);
        }
    }

    private async Task ReconnectAsync(CancellationToken ct)
    {
        _reconnectAttempt++;
        _logger.LogInformation("[Driver:{Id}] Auto-reconnect attempt #{N}...", DriverId, _reconnectAttempt);

        await OnDisconnectAsync(ct);
        var success = await PerformConnectAsync(_connectionString!, ct);

        // 重连成功后自动恢复 ScanGroup
        if (success)
            EnsureSchedulerStarted();
    }

    private int CalculateRetryDelayMs()
    {
        if (State == DriverState.Connected) return 5_000;

        var delay = (int)Math.Pow(2, _reconnectAttempt - 1) * InitialRetryIntervalMs;
        return Math.Min(delay, MaxRetryIntervalMs);
    }

    public virtual Task<bool> PingAsync(CancellationToken ct = default)
        => Task.FromResult(State == DriverState.Connected);

    // =========================================================================
    // 读写（锁下沉到 Transport 层，DriverBase 不再持有全局读写锁）
    // =========================================================================

    public async Task<UniconResponse<T>> ReadAsync<T>(UniconRequest request, CancellationToken ct = default)
    {
        if (State != DriverState.Connected)
            return UniconResponse<T>.CreateFailure($"Driver {DriverId} offline (State:{State})", 503);

        return await InternalReadAsync<T>(request, ct);
    }

    protected abstract Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct);

    public async Task<UniconResponse<bool>> WriteAsync<T>(UniconRequest request, T value, CancellationToken ct = default)
    {
        if (State != DriverState.Connected)
            return UniconResponse<bool>.CreateFailure($"Driver {DriverId} offline", 503);

        return await InternalWriteAsync(request, value, ct);
    }

    protected abstract Task<UniconResponse<bool>> InternalWriteAsync<T>(
        UniconRequest request, T value, CancellationToken ct);

    /// <summary>
    /// 默认 foreach 兜底实现；子类应 override 提供真实批量读取
    /// （地址块合并、连续地址读取、本地解析）。
    /// </summary>
    public virtual async Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(
        IEnumerable<UniconRequest> requests, CancellationToken ct = default)
    {
        var results = new List<UniconResponse<object>>();
        foreach (var req in requests)
        {
            if (ct.IsCancellationRequested) break;
            results.Add(await ReadAsync<object>(req, ct));
        }
        return results;
    }

    public virtual async Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(
        IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default)
    {
        var results = new List<UniconResponse<bool>>();
        foreach (var (req, val) in writes)
        {
            if (ct.IsCancellationRequested) break;
            results.Add(await WriteAsync(req, val, ct));
        }
        return results;
    }

    // =========================================================================
    // 订阅管理（注册时完成分组，运行时零 LINQ）
    // =========================================================================

    public virtual Task<string> SubscribeAsync(
        string address,
        Func<DataValue<object>, Task> callback,
        CancellationToken ct = default)
    {
        return SubscribeAsync(new UniconSubscription
        {
            Address    = address,
            Callback   = callback,
            ScanRateMs = DefaultScanRateMs,
            ScanMode   = DefaultScanMode
        }, ct);
    }

    public Task<string> SubscribeAsync(UniconSubscription subscription, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (string.IsNullOrWhiteSpace(subscription.Address))
            throw new ArgumentException("Address cannot be empty.", nameof(subscription));
        ArgumentNullException.ThrowIfNull(subscription.Callback);

        // 注册阶段：address 去重，加入对应 ScanGroup 的 TagEntry
        var group = _registry.GetOrCreate(subscription.ScanRateMs, subscription.ScanMode);
        var tag   = group.GetOrAddTag(subscription.Address, subscription.Metadata);
        tag.AddSubscriber(subscription.Id, subscription.Callback);

        // 路由索引，供精准取消订阅使用
        _subscriptionIndex[subscription.Id] = new SubscriptionRoute(
            subscription.Address,
            subscription.ScanRateMs,
            subscription.ScanMode,
            subscription);

        if (State == DriverState.Connected)
            EnsureSchedulerStarted();

        _logger.LogInformation(
            "[Driver:{Id}] Subscribed '{SubId}' → {Addr} (Rate:{Rate}ms, Mode:{Mode})",
            DriverId, subscription.Id, subscription.Address,
            subscription.ScanRateMs, subscription.ScanMode);

        return Task.FromResult(subscription.Id);
    }

    public virtual Task UnsubscribeAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return Task.CompletedTask;

        var toRemove = new List<string>();
        foreach (var kv in _subscriptionIndex)
        {
            if (string.Equals(kv.Value.Address, address, StringComparison.OrdinalIgnoreCase))
                toRemove.Add(kv.Key);
        }

        foreach (var id in toRemove)
            RemoveSubscription(id);

        return Task.CompletedTask;
    }

    public Task UnsubscribeByIdAsync(string subscriptionId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(subscriptionId))
            RemoveSubscription(subscriptionId);

        return Task.CompletedTask;
    }

    private void RemoveSubscription(string subscriptionId)
    {
        if (!_subscriptionIndex.TryRemove(subscriptionId, out var route)) return;

        _registry.RemoveSubscription(
            route.Address, route.ScanRateMs, route.ScanMode, subscriptionId);

        _logger.LogInformation(
            "[Driver:{Id}] Unsubscribed '{SubId}' → {Addr}",
            DriverId, subscriptionId, route.Address);
    }

    public IEnumerable<UniconSubscription> GetSubscriptions()
    {
        foreach (var kv in _subscriptionIndex)
            yield return kv.Value.Subscription;
    }

    public ScanStatistics? GetStatistics(int scanRateMs, UniconScanMode scanMode)
    {
        var group = _registry.GetOrCreate(scanRateMs, scanMode);
        return group.IsEmpty ? null : group.Statistics;
    }

    // =========================================================================
    // Scheduler 生命周期
    // =========================================================================

    private void EnsureSchedulerStarted()
    {
        if (_scheduler is not null) return;

        _schedulerCts = new CancellationTokenSource();
        _dispatcher   = new NotificationDispatcher(_logger);
        _scheduler    = new ScanScheduler(
            DriverId,
            _logger,
            _registry,
            _cacheProvider,
            _dispatcher,
            ReadBatchAsync);

        _scheduler.Start(_schedulerCts.Token);
        _logger.LogInformation("[Driver:{Id}] ScanScheduler started.", DriverId);
    }

    private async Task StopSchedulerAsync()
    {
        if (_scheduler is null) return;

        _schedulerCts?.Cancel();
        await _scheduler.DisposeAsync();

        _scheduler    = null;
        _dispatcher   = null;
        _schedulerCts?.Dispose();
        _schedulerCts = null;

        _logger.LogInformation("[Driver:{Id}] ScanScheduler stopped.", DriverId);
    }

    private void CleanupSubscriptions()
    {
        _subscriptionIndex.Clear();
        _registry.Clear();
    }

    // =========================================================================
    // IDisposable（无 Finalizer；使用标准 Dispose 模式）
    // =========================================================================

    private bool _disposed;

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (!disposing) return;

        StopWatchdog();
        // StopSchedulerAsync 是 async，Dispose 中同步等待是最后手段
        StopSchedulerAsync().GetAwaiter().GetResult();
        CleanupSubscriptions();
        _connectionLock.Dispose();
    }

    // =========================================================================
    // 内部路由记录
    // =========================================================================

    private sealed record SubscriptionRoute(
        string Address,
        int ScanRateMs,
        UniconScanMode ScanMode,
        UniconSubscription Subscription);
}
