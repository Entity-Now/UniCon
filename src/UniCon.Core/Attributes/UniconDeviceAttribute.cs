using System;
using UniCon.Core.Models;

namespace UniCon.Core.Attributes
{
    /// <summary>
    /// 标记在实体类上，用于定义南向设备物理驱动的绑定元数据 (ODM 机制 - Scenario 2)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class UniconDeviceAttribute : Attribute
    {
        /// <summary>
        /// 绑定的驱动协议类型 (如 S7, Modbus, OpcUa 等)
        /// </summary>
        public UniconDriverType DriverType { get; }

        /// <summary>
        /// 物理连接字符串
        /// </summary>
        public string ConnectionString { get; }

        /// <summary>
        /// 驱动实例全局唯一 ID (若为 null，则默认使用类名作为 DriverId)
        /// </summary>
        public string? DriverId { get; }

        public UniconDeviceAttribute(UniconDriverType driverType, string connectionString, string? driverId = null)
        {
            DriverType = driverType;
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            DriverId = driverId;
        }
    }
}
