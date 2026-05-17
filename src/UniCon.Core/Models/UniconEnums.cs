namespace UniCon.Core.Models
{
    /// <summary>
    /// 系统支持的工业通讯南向协议驱动类型枚举 (ODM 辅助 - 防止拼写错误)
    /// </summary>
    public enum UniconDriverType
    {
        /// <summary>
        /// 西门子 S7 协议驱动 (S7-300/1200/1500)
        /// </summary>
        S7,

        /// <summary>
        /// 工业标准 Modbus 协议驱动 (TCP/RTU)
        /// </summary>
        Modbus,

        /// <summary>
        /// 工业统一架构 OPC UA 协议驱动 (客户端)
        /// </summary>
        OpcUa,

        /// <summary>
        /// 消息队列遥测传输 MQTT 协议驱动 (发布/订阅)
        /// </summary>
        Mqtt,

        /// <summary>
        /// OPC UA 订阅发布组播协议驱动 (无连接)
        /// </summary>
        OpcUaPubSub
    }

    /// <summary>
    /// 北向映射强类型提示枚举 (ODM 辅助 - 确立强类型约束)
    /// </summary>
    public enum UniconDataType
    {
        /// <summary>
        /// 通用未指定对象类型 (由底自动推导或默认为原始字串)
        /// </summary>
        Object,

        /// <summary>
        /// 单字节无符号整数 (8位)
        /// </summary>
        Byte,

        /// <summary>
        /// 双字节有符号短整数 (16位)
        /// </summary>
        Int16,

        /// <summary>
        /// 四字节有符号标准整数 (32位)
        /// </summary>
        Int32,

        /// <summary>
        /// 单精度浮点数 (32位)
        /// </summary>
        Float,

        /// <summary>
        /// 双精度浮点数 (64位)
        /// </summary>
        Double,

        /// <summary>
        /// 布尔状态值 (开/关)
        /// </summary>
        Boolean,

        /// <summary>
        /// 字符串/文本值
        /// </summary>
        String
    }
}
