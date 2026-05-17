using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// 周期全量轮询策略：每次扫描周期无条件触发通知，无论值是否变化。
/// </summary>
internal sealed class PolledScanStrategy : IScanStrategy
{
    public static readonly IScanStrategy Instance = new PolledScanStrategy();

    private PolledScanStrategy() { }

    public bool ShouldNotify(DataValue<object> newValue, DataValue<object>? cachedValue, TagMetadata? metadata)
        => true;
}
