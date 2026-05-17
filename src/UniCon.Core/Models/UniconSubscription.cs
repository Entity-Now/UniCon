using System;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Models;

/// <summary>订阅满队时的溢出处理策略</summary>
public enum OverflowPolicy
{
    /// <summary>丢弃队列中最旧的数据，保留最新值（默认）</summary>
    DropOldest,

    /// <summary>丢弃新到达的数据，保护历史时序完整性</summary>
    DropNewest
}

/// <summary>
/// 结构化订阅描述，注册后由 ScanGroupRegistry 映射到对应的 ScanGroup。
/// Callback 为 async Func，防止同步回调阻塞通知线程。
/// Subscription 本身不参与轮询，不持有 LastPolledTime。
/// </summary>
public sealed class UniconSubscription
{
    /// <summary>全局唯一订阅标识符</summary>
    public string Id { get; } = Guid.NewGuid().ToString();

    /// <summary>寄存器绝对地址，如 "DB1.DBD0" 或 "holding:1"</summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>轮询刷新周期（毫秒）</summary>
    public int ScanRateMs { get; init; } = 1000;

    /// <summary>扫描通知策略</summary>
    public UniconScanMode ScanMode { get; init; } = UniconScanMode.ExceptionBased;

    /// <summary>
    /// 异步通知回调，由 NotificationDispatcher 在独立线程中 await。
    /// 使用 Func&lt;DataValue&lt;object&gt;, Task&gt; 防止同步阻塞扫描线程。
    /// </summary>
    public Func<DataValue<object>, Task> Callback { get; init; } = null!;

    /// <summary>通知队列最大长度（Backpressure 控制）</summary>
    public int MaxQueueLength { get; init; } = 128;

    /// <summary>队列满时的溢出策略</summary>
    public OverflowPolicy OverflowPolicy { get; init; } = OverflowPolicy.DropOldest;

    /// <summary>可选的 Tag 静态元数据（用于死区过滤、类型描述等）</summary>
    public TagMetadata? Metadata { get; init; }
}
