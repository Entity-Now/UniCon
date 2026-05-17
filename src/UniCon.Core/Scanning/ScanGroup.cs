using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// 以 ScanRate 为粒度的扫描组，内部维护 Address → TagEntry 映射。
/// 注册时完成地址去重；LastPolledTime 属于 ScanGroup 而非 Subscription。
/// </summary>
internal sealed class ScanGroup
{
    private readonly ConcurrentDictionary<string, TagEntry> _tags =
        new(StringComparer.OrdinalIgnoreCase);

    public int ScanRateMs { get; }
    public UniconScanMode ScanMode { get; }
    public IScanStrategy Strategy { get; }
    public ScanStatistics Statistics { get; } = new();

    /// <summary>本组上一次被调度执行的 UTC 时间</summary>
    public DateTime LastPolledTime { get; set; } = DateTime.MinValue;

    public ScanGroup(int scanRateMs, UniconScanMode scanMode)
    {
        ScanRateMs = scanRateMs;
        ScanMode   = scanMode;
        Strategy   = scanMode == UniconScanMode.Polled
            ? PolledScanStrategy.Instance
            : ExceptionBasedScanStrategy.Instance;
    }

    public TagEntry GetOrAddTag(string address, TagMetadata? metadata = null)
        => _tags.GetOrAdd(address, addr => new TagEntry(addr, metadata));

    public void RemoveSubscriber(string address, string subscriptionId)
    {
        if (!_tags.TryGetValue(address, out var entry)) return;

        entry.RemoveSubscriber(subscriptionId);

        // 该地址无订阅者时从组内移除，避免空轮询
        if (!entry.HasSubscribers)
            _tags.TryRemove(address, out _);
    }

    public bool IsEmpty => _tags.IsEmpty;

    public bool IsDue(DateTime now)
        => (now - LastPolledTime).TotalMilliseconds >= ScanRateMs;

    /// <summary>
    /// 返回下一次到期的剩余毫秒数。若已到期返回 0。
    /// </summary>
    public int MillisecondsUntilDue(DateTime now)
    {
        var remaining = ScanRateMs - (int)(now - LastPolledTime).TotalMilliseconds;
        return Math.Max(0, remaining);
    }

    public ICollection<TagEntry> GetTags() => _tags.Values;
}
