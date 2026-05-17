namespace UniCon.Core.Models;

/// <summary>
/// Tag 点位读写权限
/// </summary>
public enum TagAccessMode
{
    ReadOnly,
    WriteOnly,
    ReadWrite
}

/// <summary>
/// Tag 点位的静态元数据描述，注册时绑定，运行期只读
/// </summary>
public sealed record TagMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public UniconDataType DataType { get; init; } = UniconDataType.Object;
    public TagAccessMode AccessMode { get; init; } = TagAccessMode.ReadOnly;

    /// <summary>工程单位，如 "℃"、"bar"、"rpm"</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>线性缩放系数：物理值 = RawValue * ScalingFactor + ScalingOffset</summary>
    public double ScalingFactor { get; init; } = 1.0;
    public double ScalingOffset { get; init; } = 0.0;

    /// <summary>
    /// 变化死区阈值（工程单位）。
    /// ExceptionBased 模式下，数值变化量 ≤ Deadband 则不触发通知，避免微小浮点抖动。
    /// 0 或负值表示禁用死区。
    /// </summary>
    public double Deadband { get; init; } = 0.0;

    public string Description { get; init; } = string.Empty;
}
