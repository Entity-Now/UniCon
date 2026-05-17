using System;

namespace UniCon.Core.Models
{
    /// <summary>
    /// 表示一个主动扫描与刷新订阅的配置项 (RULE 2.3)
    /// </summary>
    public class UniconSubscription
    {
        /// <summary>
        /// 订阅的唯一标识符
        /// </summary>
        public string Id { get; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 寄存器绝对地址，如 "DB1.DBD0" 或 "holding:1"
        /// </summary>
        public string Address { get; init; } = string.Empty;

        /// <summary>
        /// 轮询刷新周期 (毫秒)
        /// </summary>
        public int ScanRateMs { get; init; } = 1000;

        /// <summary>
        /// 刷新推送模式
        /// </summary>
        public UniconScanMode ScanMode { get; init; } = UniconScanMode.ExceptionBased;

        /// <summary>
        /// 数值/状态变更时的通知回调
        /// </summary>
        public Action<DataValue<object>> Callback { get; init; } = null!;

        /// <summary>
        /// 内部使用的上一次轮询调度时间戳
        /// </summary>
        internal DateTime LastPolledTime { get; set; } = DateTime.MinValue;
    }
}
