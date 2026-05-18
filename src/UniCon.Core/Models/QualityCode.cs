namespace UniCon.Core.Models;

/// <summary>
/// OPC UA / IEC 62541 兼容质量代码，描述采样数据的可信度与来源状态
/// </summary>
public enum QualityCode : ushort
{
    Good = 0x0000,
    GoodLocalOverride = 0x00D8,
    Uncertain = 0x4000,
    UncertainLastUsable = 0x4040,
    UncertainSubstitute = 0x44B0,
    Bad = 0x8000,
    BadNoCommunication = 0x8070,
    BadDeviceFailure = 0x80B0,
    BadConfigurationError = 0x8020,
    BadOutOfService = 0x8080,
    BadWaitingForInitialData = 0x8030,
}
