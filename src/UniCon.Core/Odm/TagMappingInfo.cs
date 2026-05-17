using System;

namespace UniCon.Core.Odm
{
    /// <summary>
    /// 结构化点位映射信息：用于存储反射扫描提取的点位元数据 (RULE 4.0)
    /// </summary>
    public record TagMappingInfo(
        string PropertyName,
        string Address,
        string TypeHint,
        Type PropertyType,
        bool IsWritable
    );
}
