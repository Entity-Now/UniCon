using System.Collections.Concurrent;
using System.Collections.Generic;
using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// ScanGroup 注册表，以 (ScanRateMs, ScanMode) 为复合 Key 维护所有扫描组。
/// 订阅注册阶段完成分组，调度阶段只遍历活跃组，避免运行时 GroupBy。
/// </summary>
internal sealed class ScanGroupRegistry
{
    private readonly ConcurrentDictionary<(int RateMs, UniconScanMode Mode), ScanGroup> _groups = new();

    /// <summary>
    /// 获取已有扫描组，不存在则创建。
    /// </summary>
    public ScanGroup GetOrCreate(int scanRateMs, UniconScanMode scanMode)
        => _groups.GetOrAdd((scanRateMs, scanMode), key => new ScanGroup(key.RateMs, key.Mode));

    /// <summary>
    /// 移除指定订阅，若该组变为空则从注册表中删除。
    /// </summary>
    public void RemoveSubscription(string address, int scanRateMs, UniconScanMode scanMode, string subscriptionId)
    {
        var key = (scanRateMs, scanMode);
        if (!_groups.TryGetValue(key, out var group)) return;

        group.RemoveSubscriber(address, subscriptionId);

        if (group.IsEmpty)
            _groups.TryRemove(key, out _);
    }

    public IEnumerable<ScanGroup> GetAll() => _groups.Values;

    public void Clear() => _groups.Clear();
}
