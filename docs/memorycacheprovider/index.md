---
url: /docs/memorycacheprovider/index.md
---
# MemoryCacheProvider

## 概述 (Overview)

`MemoryCacheProvider` 是 UniCon 框架的内存缓存实现，遵循 `IUniconCacheProvider` 接口。它基于 .NET `ConcurrentDictionary` 提供线程安全的高并发读写，适用于轻量级本地缓存场景。

## 使用方法 (Usage)

在依赖注入阶段注册缓存提供者：

```csharp
var builder = WebApplication.CreateBuilder(args);
// 注册内存缓存实现
builder.Services.AddSingleton<IUniconCacheProvider, MemoryCacheProvider>();
```

> **注意**：如果同时注册了 `RedisCacheProviderPlaceholder`，`MemoryCacheProvider` 将作为默认实现使用，优先级更高。

## 参数说明 (Parameters)

> **MemoryCacheProvider** 本身不接受外部配置参数，所有行为均通过代码实现。

## 返回值 (Returns)

| 类型 | 说明 |
|------|------|
| `IUniconCacheProvider` | 已注册的缓存提供者实例 |

## 使用示例 (Examples)

```csharp
public class CacheService
{
    private readonly IUniconCacheProvider _cacheProvider;
    public CacheService(IUniconCacheProvider cacheProvider)
    {
        _cacheProvider = cacheProvider;
    }

    public async Task<DataValue<object>?> GetDriverCacheAsync(string driverId, string address)
    {
        return await _cacheProvider.GetAsync(driverId, address);
    }

    public async Task SetDriverCacheAsync(string driverId, string address, DataValue<object> value)
    {
        await _cacheProvider.SetAsync(driverId, address, value);
    }

    public async Task RemoveDriverCacheAsync(string driverId, string address)
    {
        await _cacheProvider.RemoveAsync(driverId, address);
    }
}
```

***

> **更新记录**：文档已同步至 `src/UniCon.Core/Caching/MemoryCacheProvider.cs`（v2.3），确保方法签名与示例代码保持一致。
