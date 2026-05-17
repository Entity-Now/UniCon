using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UniCon.Core.Caching;
using UniCon.Core.Models;

namespace UniCon.Core
{
    /// <summary>
    /// 驱动抽象基类，具备自愈机制 (Watchdog)、并发控制与状态管理
    /// </summary>
    public abstract class DriverBase : IUniconDriver
    {
        protected readonly ILogger _logger;
        protected readonly SemaphoreSlim _syncLock = new(1, 1);
        protected readonly SemaphoreSlim _connectionLock = new(1, 1);

        private string? _connectionString;
        private CancellationTokenSource? _watchdogCts;
        private int _reconnectAttempt = 0;

        public string DriverId { get; protected set; }
        public string? ConnectionString => _connectionString;
        public virtual DriverState State { get; protected set; } = DriverState.Disconnected;
        public bool IsConnected => State == DriverState.Connected;

        // 重连配置 (可根据需要开放给构造函数或属性)
        public bool EnableAutoReconnect { get; set; } = true;
        public int MaxRetryIntervalMs { get; set; } = 30000;
        public int InitialRetryIntervalMs { get; set; } = 1000;

        // 统一缓存与主动扫描配置 (RULE 2.2)
        public static IUniconCacheProvider CacheProvider { get; set; } = new MemoryCacheProvider();
        public UniconScanMode DefaultScanMode { get; set; } = UniconScanMode.ExceptionBased;
        public int DefaultScanRateMs { get; set; } = 1000;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, UniconSubscription> _subscriptions = new();
        private CancellationTokenSource? _schedulerCts;
        private Task? _schedulerTask;

        protected DriverBase(string driverId, ILogger logger)
        {
            DriverId = driverId;
            _logger = logger;
        }

        public async Task<bool> ConnectAsync(string connectionString, CancellationToken ct = default)
        {
            await _connectionLock.WaitAsync(ct);
            try
            {
                _connectionString = connectionString;
                var success = await PerformConnectInternal(connectionString, ct);

                if (success && EnableAutoReconnect && _watchdogCts == null)
                {
                    StartWatchdog();
                }

                return success;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        private async Task<bool> PerformConnectInternal(string connectionString, CancellationToken ct)
        {
            if (State == DriverState.Connected) return true;

            _logger.LogInformation($"[Driver:{DriverId}] Attempting to connect...");
            State = DriverState.Connecting;

            try
            {
                var success = await OnConnectAsync(connectionString, ct);
                if (success)
                {
                    State = DriverState.Connected;
                    _reconnectAttempt = 0;
                    _logger.LogInformation($"[Driver:{DriverId}] Connected successfully.");
                    if (!_subscriptions.IsEmpty)
                    {
                        StartScheduler();
                    }
                }
                else
                {
                    State = DriverState.Faulted;
                }
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Driver:{DriverId}] Connection failed.");
                State = DriverState.Faulted;
                return false;
            }
        }

        protected abstract Task<bool> OnConnectAsync(string connectionString, CancellationToken ct);

        public async Task DisconnectAsync(CancellationToken ct = default)
        {
            StopWatchdog();
            StopScheduler();
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

        private void StartWatchdog()
        {
            _watchdogCts = new CancellationTokenSource();
            Task.Run(() => WatchdogLoopAsync(_watchdogCts.Token));
            _logger.LogDebug($"[Driver:{DriverId}] Watchdog started.");
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
                    if (State == DriverState.Faulted || State == DriverState.Disconnected)
                    {
                        await ReconnectAsync(ct);
                    }
                    else if (State == DriverState.Connected)
                    {
                        var alive = await PingAsync(ct);
                        if (!alive)
                        {
                            _logger.LogWarning($"[Driver:{DriverId}] Heartbeat failed. Marking as Faulted.");
                            State = DriverState.Faulted;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[Driver:{DriverId}] Watchdog error.");
                }

                var delay = CalculateRetryDelay();
                await Task.Delay(delay, ct);
            }
        }

        private async Task ReconnectAsync(CancellationToken ct)
        {
            _reconnectAttempt++;
            _logger.LogInformation($"[Driver:{DriverId}] Auto-reconnecting (Attempt: {_reconnectAttempt})...");

            // 重连前彻底清理旧资源
            await OnDisconnectAsync(ct);
            await PerformConnectInternal(_connectionString!, ct);
        }

        private int CalculateRetryDelay()
        {
            if (State == DriverState.Connected) return 5000; // 正常状态下 5s 检查一次心跳

            // 指数退避算法 (RULE 2.2)
            var delay = (int)Math.Pow(2, _reconnectAttempt - 1) * InitialRetryIntervalMs;
            return Math.Min(delay, MaxRetryIntervalMs);
        }

        public virtual async Task<bool> PingAsync(CancellationToken ct = default)
        {
            return await Task.FromResult(State == DriverState.Connected);
        }

        public async Task<UniconResponse<T>> ReadAsync<T>(UniconRequest request, CancellationToken ct = default)
        {
            // 离线快速返回 (RULE 1.2)
            if (State != DriverState.Connected)
                return UniconResponse<T>.CreateFailure($"Driver {DriverId} is offline (State: {State})", 503);

            await _syncLock.WaitAsync(ct);
            try
            {
                return await InternalReadAsync<T>(request, ct);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        protected abstract Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct);

        public async Task<UniconResponse<bool>> WriteAsync<T>(UniconRequest request, T value, CancellationToken ct = default)
        {
            if (State != DriverState.Connected)
                return UniconResponse<bool>.CreateFailure($"Driver {DriverId} is offline", 503);

            await _syncLock.WaitAsync(ct);
            try
            {
                return await InternalWriteAsync(request, value, ct);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        protected abstract Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct);

        public virtual async Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(IEnumerable<UniconRequest> requests, CancellationToken ct = default)
        {
            var results = new List<UniconResponse<object>>();
            foreach (var req in requests)
            {
                if (ct.IsCancellationRequested) break;
                results.Add(await ReadAsync<object>(req, ct));
            }
            return results;
        }

        public virtual async Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default)
        {
            var results = new List<UniconResponse<bool>>();
            foreach (var item in writes)
            {
                if (ct.IsCancellationRequested) break;
                results.Add(await WriteAsync(item.Request, item.Value, ct));
            }
            return results;
        }

        public virtual Task SubscribeAsync(string address, Action<DataValue<object>> callback, CancellationToken ct = default)
        {
            return SubscribeAsync(new UniconSubscription
            {
                Address = address,
                Callback = callback,
                ScanRateMs = DefaultScanRateMs,
                ScanMode = DefaultScanMode
            }, ct);
        }

        public virtual Task UnsubscribeAsync(string address, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(address)) return Task.CompletedTask;

            var targetSubs = _subscriptions.Values
                .Where(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (targetSubs.Count > 0)
            {
                foreach (var sub in targetSubs)
                {
                    _subscriptions.TryRemove(sub.Id, out _);
                }
                _logger.LogInformation($"Stopped active polling subscriptions for address: {address} on driver: {DriverId}");
            }
            else
            {
                _subscriptions.TryRemove(address, out _);
            }

            return Task.CompletedTask;
        }

        public virtual Task<string> SubscribeAsync(UniconSubscription subscription, CancellationToken ct = default)
        {
            if (subscription == null) throw new ArgumentNullException(nameof(subscription));
            if (string.IsNullOrWhiteSpace(subscription.Address)) throw new ArgumentNullException(nameof(subscription.Address));
            if (subscription.Callback == null) throw new ArgumentNullException(nameof(subscription.Callback));

            _subscriptions[subscription.Id] = subscription;

            if (State == DriverState.Connected && _schedulerTask == null)
            {
                StartScheduler();
            }

            _logger.LogInformation($"Successfully registered structured subscription '{subscription.Id}' for address: {subscription.Address} (Mode: {subscription.ScanMode}, Rate: {subscription.ScanRateMs}ms) on driver: {DriverId}");
            return Task.FromResult(subscription.Id);
        }

        public virtual Task UnsubscribeByIdAsync(string subscriptionId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(subscriptionId)) return Task.CompletedTask;

            if (_subscriptions.TryRemove(subscriptionId, out var sub))
            {
                _logger.LogInformation($"Stopped structured subscription '{subscriptionId}' for address: {sub.Address} on driver: {DriverId}");
            }
            return Task.CompletedTask;
        }

        public virtual IEnumerable<UniconSubscription> GetSubscriptions()
        {
            return _subscriptions.Values;
        }

        private void StartScheduler()
        {
            if (_schedulerTask != null) return;

            _schedulerCts = new CancellationTokenSource();
            _schedulerTask = Task.Run(() => RunSchedulerLoopAsync(_schedulerCts.Token));
            _logger.LogInformation($"Started master subscription scheduler for driver: {DriverId}");
        }

        private void StopScheduler()
        {
            if (_schedulerTask == null) return;

            _schedulerCts?.Cancel();
            try
            {
                _schedulerTask?.Wait(500);
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Error waiting for master scheduler to stop: {ex.Message}");
            }
            _schedulerCts?.Dispose();
            _schedulerCts = null;
            _schedulerTask = null;
            _logger.LogInformation($"Stopped master subscription scheduler for driver: {DriverId}");
        }

        private async Task RunSchedulerLoopAsync(CancellationToken ct)
        {
            const int TickResolutionMs = 50;

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (State != DriverState.Connected)
                    {
                        await Task.Delay(500, ct);
                        continue;
                    }

                    var now = DateTime.UtcNow;
                    var dueSubscriptions = _subscriptions.Values
                        .Where(s => (now - s.LastPolledTime).TotalMilliseconds >= s.ScanRateMs)
                        .ToList();

                    if (dueSubscriptions.Count > 0)
                    {
                        var addressGroups = dueSubscriptions.GroupBy(s => s.Address).ToList();
                        var requests = addressGroups.Select(g => new UniconRequest { Address = g.Key }).ToList();

                        var batchResults = await ReadBatchAsync(requests, ct);
                        var resultsList = batchResults.ToList();
                        var resultsDict = new Dictionary<string, UniconResponse<object>>();

                        for (int i = 0; i < requests.Count; i++)
                        {
                            if (i < resultsList.Count)
                            {
                                resultsDict[requests[i].Address] = resultsList[i];
                            }
                        }

                        foreach (var group in addressGroups)
                        {
                            var address = group.Key;
                            if (resultsDict.TryGetValue(address, out var readResult) && readResult.Success && readResult.Data != null)
                            {
                                var newValue = readResult.Data;

                                foreach (var sub in group)
                                {
                                    sub.LastPolledTime = now;
                                    bool shouldNotify = false;

                                    if (sub.ScanMode == UniconScanMode.Polled)
                                    {
                                        shouldNotify = true;
                                    }
                                    else
                                    {
                                        var cachedValue = await CacheProvider.GetAsync(DriverId, address, ct);
                                        if (cachedValue == null)
                                        {
                                            shouldNotify = true;
                                        }
                                        else
                                        {
                                            bool valueChanged = !Equals(cachedValue.Value, newValue.Value);
                                            bool statusChanged = cachedValue.Status != newValue.Status;

                                            if (valueChanged || statusChanged)
                                            {
                                                shouldNotify = true;
                                            }
                                        }
                                    }

                                    if (shouldNotify)
                                    {
                                        await CacheProvider.SetAsync(DriverId, address, newValue, ct);

                                        try
                                        {
                                            sub.Callback(newValue);
                                        }
                                        catch (Exception callbackEx)
                                        {
                                            _logger.LogError($"Subscriber callback threw exception for address '{address}' in sub '{sub.Id}': {callbackEx.Message}");
                                        }
                                    }
                                }
                            }
                            else
                            {
                                foreach (var sub in group)
                                {
                                    sub.LastPolledTime = now;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in master subscription scheduler loop for driver {DriverId}: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TickResolutionMs, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        protected void CleanupSubscriptions()
        {
            StopScheduler();
            _subscriptions.Clear();
        }

        ~DriverBase()
        {
            CleanupSubscriptions();
        }

        public abstract void Dispose();
    }
}
