using System;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Notification;

/// <summary>
/// 待分发通知的不可变载体，由 ScanScheduler 生产，由 NotificationDispatcher 消费。
/// </summary>
internal sealed record NotificationEnvelope(
    string SubscriptionId,
    string Address,
    DataValue<object> Value,
    Func<DataValue<object>, Task> Callback);
