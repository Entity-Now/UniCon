using System;
using UniCon.Core.Models;

namespace UniCon.Core.Attributes
{
    /// <summary>
    /// 标记在属性上，定义与底层驱动点位地址的映射关系 (ODM 机制 - Scenario 1 & 2)
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class UniconAddressAttribute : Attribute
    {
        /// <summary>
        /// 底层物理通道点位地址
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// 目标映射强类型枚举值
        /// </summary>
        public UniconDataType Type { get; }

        /// <summary>
        /// 是否允许北向写入 (默认为 true)
        /// </summary>
        public bool IsWritable { get; set; } = true;

        public UniconAddressAttribute(string address, UniconDataType type = UniconDataType.Object)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
            Type = type;
        }
    }
}
