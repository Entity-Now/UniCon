using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Scanning;

/// <summary>
/// 代表单个物理地址的采集单元，持有该地址的最新缓存值及所有订阅者回调。
/// 注册阶段完成地址去重；一个地址只读一次，对应多个回调。
/// </summary>
internal sealed class TagEntry
{
    private readonly ConcurrentDictionary<string, Func<DataValue<object>, Task>> _subscribers = new();

    public string Address { get; }
    public TagMetadata? Metadata { get; }

    /// <summary>Tag 层缓存，CacheCompare 在此层进行，而非在 Subscription 层</summary>
    public DataValue<object>? LastValue { get; private set; }

    public TagEntry(string address, TagMetadata? metadata = null)
    {
        Address = address;
        Metadata = metadata;
    }

    public void AddSubscriber(string subscriptionId, Func<DataValue<object>, Task> callback)
        => _subscribers[subscriptionId] = callback;

    public bool RemoveSubscriber(string subscriptionId)
        => _subscribers.TryRemove(subscriptionId, out _);

    public bool HasSubscribers => !_subscribers.IsEmpty;

    /// <summary>迭代当前所有订阅者，避免在高频路径上创建临时 List</summary>
    public IEnumerable<KeyValuePair<string, Func<DataValue<object>, Task>>> GetSubscribers()
        => _subscribers;

    public void UpdateCache(DataValue<object> value) => LastValue = value;
}
