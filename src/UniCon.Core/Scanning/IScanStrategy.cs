using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// 扫描通知策略接口，决定给定新值是否需要向订阅者推送通知。
/// 通过策略模式替代 if(ScanMode == ...) 分支，支持扩展自定义策略。
/// </summary>
public interface IScanStrategy
{
    bool ShouldNotify(DataValue<object> newValue, DataValue<object>? cachedValue, TagMetadata? metadata);
}
