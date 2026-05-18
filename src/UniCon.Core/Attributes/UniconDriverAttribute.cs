using System;

namespace UniCon.Core
{
    /// <summary>
    /// 标记在工业通讯驱动类上，定义其别名简称并启用自动装配扫描 (v2.3)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class UniconDriverAttribute : Attribute
    {
        /// <summary>
        /// 驱动别名简称（例如 "S7", "Modbus", "OpcUa"）
        /// </summary>
        public string DriverType { get; }

        public UniconDriverAttribute(string driverType)
        {
            if (string.IsNullOrWhiteSpace(driverType))
                throw new ArgumentException("Driver type alias cannot be empty.", nameof(driverType));

            DriverType = driverType;
        }
    }
}
