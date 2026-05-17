using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;
using UniCon.Core.Scanning;

namespace UniCon.Core;

/// <summary>驱动运行状态</summary>
public enum DriverState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted,
    Disconnecting
}

/// <summary>驱动状态变更事件参数</summary>
public sealed record DriverStateChangedEventArgs(
    string DriverId,
    DriverState OldState,
    DriverState NewState);

/// <summary>
/// UniCon 统一南向驱动接口 v2。
/// 订阅使用异步 Func callback；批量读写交由具体 Driver 实现真实批量；
/// 提供状态变更事件与统计查询。
/// </summary>
public interface IUniconDriver : IDisposable
{
    string  DriverId         { get; }
    string? ConnectionString { get; }
    DriverState State        { get; }
    bool IsConnected         => State == DriverState.Connected;

    /// <summary>驱动状态发生变更时触发</summary>
    event EventHandler<DriverStateChangedEventArgs>? StateChanged;

    Task<bool> ConnectAsync(string connectionString, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
    Task<bool> PingAsync(CancellationToken ct = default);

    Task<UniconResponse<T>>    ReadAsync<T>(UniconRequest request, CancellationToken ct = default);
    Task<UniconResponse<bool>> WriteAsync<T>(UniconRequest request, T value, CancellationToken ct = default);

    /// <summary>真实批量读取，由具体 Driver 实现；DriverBase 提供 foreach 兜底</summary>
    Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(
        IEnumerable<UniconRequest> requests, CancellationToken ct = default);

    Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(
        IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default);

    /// <summary>
    /// 快捷订阅：异步 callback，使用驱动默认扫描配置
    /// </summary>
    Task<string> SubscribeAsync(
        string address,
        Func<DataValue<object>, Task> callback,
        CancellationToken ct = default);

    /// <summary>注册结构化订阅，返回订阅 ID</summary>
    Task<string> SubscribeAsync(UniconSubscription subscription, CancellationToken ct = default);

    Task UnsubscribeAsync(string address, CancellationToken ct = default);
    Task UnsubscribeByIdAsync(string subscriptionId, CancellationToken ct = default);

    IEnumerable<UniconSubscription> GetSubscriptions();

    /// <summary>查询指定 ScanGroup 的运行统计</summary>
    ScanStatistics? GetStatistics(int scanRateMs, UniconScanMode scanMode);
}
