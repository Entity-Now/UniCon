using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core
{
    /// <summary>
    /// 驱动运行状态
    /// </summary>
    public enum DriverState
    {
        Disconnected,
        Connecting,
        Connected,
        Faulted,
        Disconnecting
    }

    /// <summary>
    /// 增强型统一驱动接口 (Architecture Hardening)
    /// </summary>
    public interface IUniconDriver : IDisposable
    {
        string DriverId { get; }

        /// <summary>
        /// 驱动当前所关联的物理连接字符串
        /// </summary>
        string? ConnectionString { get; }

        /// <summary>
        /// 细化的驱动状态
        /// </summary>
        DriverState State { get; }

        bool IsConnected => State == DriverState.Connected;

        Task<bool> ConnectAsync(string connectionString, CancellationToken ct = default);
        Task DisconnectAsync(CancellationToken ct = default);

        /// <summary>
        /// 心跳探活
        /// </summary>
        Task<bool> PingAsync(CancellationToken ct = default);

        Task<UniconResponse<T>> ReadAsync<T>(UniconRequest request, CancellationToken ct = default);
        Task<UniconResponse<bool>> WriteAsync<T>(UniconRequest request, T value, CancellationToken ct = default);

        Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(IEnumerable<UniconRequest> requests, CancellationToken ct = default);
        Task<IEnumerable<UniconResponse<bool>>> WriteBatchAsync(IEnumerable<(UniconRequest Request, object Value)> writes, CancellationToken ct = default);

        Task SubscribeAsync(string address, Action<DataValue<object>> callback, CancellationToken ct = default);
        Task UnsubscribeAsync(string address, CancellationToken ct = default);

        /// <summary>
        /// 注册一个高效率、高定制的结构化订阅，返回订阅 ID
        /// </summary>
        Task<string> SubscribeAsync(UniconSubscription subscription, CancellationToken ct = default);

        /// <summary>
        /// 根据订阅 ID 取消指定订阅
        /// </summary>
        Task UnsubscribeByIdAsync(string subscriptionId, CancellationToken ct = default);

        /// <summary>
        /// 获取当前所有活动订阅列表
        /// </summary>
        IEnumerable<UniconSubscription> GetSubscriptions();
    }
}
