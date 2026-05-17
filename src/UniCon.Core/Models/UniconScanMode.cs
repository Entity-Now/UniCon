namespace UniCon.Core.Models
{
    /// <summary>
    /// 南向非订阅协议通讯驱动的主动扫描与刷新推送模式 (RULE 3.1)
    /// </summary>
    public enum UniconScanMode
    {
        /// <summary>
        /// 异常值变更检测模式（默认，最优）：
        /// 驱动按指定的 ScanRate 周期轮询物理寄存器，但只有在值内容发生变化或质量状态改变时，才刷新缓存并向上推送。
        /// 适合：DB块点位较多、变化较慢、北向订阅链路注重低带宽的工业场景。
        /// </summary>
        ExceptionBased,

        /// <summary>
        /// 周期全量轮询更新模式：
        /// 驱动按指定的 ScanRate 周期性轮询物理寄存器，无论读取出的数值是否发生改变，每周期均强制覆盖刷新缓存并向上推送回调。
        /// 适合：必须严格保障物理设备活跃状态、实时获取定时时序序列的应用场景。
        /// </summary>
        Polled
    }
}
