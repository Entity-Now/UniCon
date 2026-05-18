using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniCon.Drivers.OpcUaPubSub.Transports;

/// <summary>
/// 底层传输接口抽象，屏蔽 UDP 和 MQTT 差异
/// </summary>
public interface IPubSubTransport : IAsyncDisposable
{
    /// <summary>
    /// 当接收到新的数据载荷时触发
    /// </summary>
    event EventHandler<byte[]>? OnMessageReceived;

    /// <summary>
    /// 当传输连接丢失时触发
    /// </summary>
    event EventHandler? ConnectionLost;

    /// <summary>
    /// 连接/加入组播
    /// </summary>
    Task ConnectAsync(Uri uri, CancellationToken ct = default);

    /// <summary>
    /// 断开/离开组播
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);
}
