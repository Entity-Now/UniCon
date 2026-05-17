using UniCon.Core.Attributes;
using UniCon.Core.Models;

namespace UniCon.WebServer.Models
{
    /// <summary>
    /// 工业生产线实体数据模型 (ODM 映射机制示例 - Scenario 1 & 2)
    /// </summary>
    [UniconDevice(UniconDriverType.S7, "CpuType=S71200;Ip=192.168.0.100;Rack=0;Slot=1", driverId: "PLC_ODM_01")]
    public class ProductionLineEntity
    {
        /// <summary>
        /// 反应釜温度
        /// </summary>
        [UniconAddress("DB1.DBD0", UniconDataType.Float)]
        public float Temperature { get; set; }

        /// <summary>
        /// 反应釜压力
        /// </summary>
        [UniconAddress("DB1.DBD4", UniconDataType.Float)]
        public float Pressure { get; set; }

        /// <summary>
        /// 生产线运行状态 (只读，不支持北向写入)
        /// </summary>
        [UniconAddress("DB1.DBX8.0", UniconDataType.Boolean)]
        public bool IsRunning { get; set; }

        /// <summary>
        /// 当日累计产量
        /// </summary>
        [UniconAddress("DB1.DBD10", UniconDataType.Int32)]
        public int ProductCount { get; set; }
    }
}
