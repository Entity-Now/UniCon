using System;

namespace UniCon.Core.Network
{
    /// <summary>
    /// 全局网络可用性检测接口
    /// </summary>
    public interface INetworkMonitor
    {
        /// <summary>
        /// 获取当前系统网络是否可用
        /// </summary>
        bool IsNetworkAvailable { get; }

        /// <summary>
        /// 当系统网络可用性发生改变时触发
        /// </summary>
        event EventHandler<bool>? NetworkAvailabilityChanged;
    }
}
