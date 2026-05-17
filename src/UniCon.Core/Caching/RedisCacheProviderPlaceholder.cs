using System;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Caching
{
    /// <summary>
    /// 兼容 Redis 缓存实现的架构代理结构 (RULE 2.2)
    /// 可由集成人员注入 StackExchange.Redis 的 IDatabase 并挂载，实现分布式工控网关缓存共享。
    /// 若不传入具体数据库实例，则自动平滑回退至高可用本地内存，保障系统零阻碍开箱即用。
    /// </summary>
    public class RedisCacheProviderPlaceholder : IUniconCacheProvider
    {
        private readonly object? _redisDatabase; // 代理 StackExchange.Redis.IDatabase 句柄
        private readonly IUniconCacheProvider _fallbackMemory;

        public RedisCacheProviderPlaceholder(object? redisDatabase = null)
        {
            _redisDatabase = redisDatabase;
            _fallbackMemory = new MemoryCacheProvider();
        }

        public async Task<DataValue<object>?> GetAsync(string driverId, string address, CancellationToken ct = default)
        {
            if (_redisDatabase == null)
            {
                return await _fallbackMemory.GetAsync(driverId, address, ct);
            }

            // -------------------------------------------------------------
            // StackExchange.Redis 物理集成伪代码示例如下（生产环境可直接取消注释并替换）：
            // -------------------------------------------------------------
            // string key = $"unicon:cache:{driverId}:{address}";
            // var db = (StackExchange.Redis.IDatabase)_redisDatabase;
            // string? json = await db.StringGetAsync(key);
            // return json != null ? System.Text.Json.JsonSerializer.Deserialize<DataValue<object>>(json) : null;

            return await _fallbackMemory.GetAsync(driverId, address, ct);
        }

        public async Task SetAsync(string driverId, string address, DataValue<object> dataValue, CancellationToken ct = default)
        {
            if (_redisDatabase == null)
            {
                await _fallbackMemory.SetAsync(driverId, address, dataValue, ct);
                return;
            }

            // -------------------------------------------------------------
            // StackExchange.Redis 物理集成伪代码示例如下（生产环境可直接取消注释并替换）：
            // -------------------------------------------------------------
            // string key = $"unicon:cache:{driverId}:{address}";
            // string json = System.Text.Json.JsonSerializer.Serialize(dataValue);
            // var db = (StackExchange.Redis.IDatabase)_redisDatabase;
            // await db.StringSetAsync(key, json, TimeSpan.FromHours(24));

            await _fallbackMemory.SetAsync(driverId, address, dataValue, ct);
        }

        public async Task RemoveAsync(string driverId, string address, CancellationToken ct = default)
        {
            if (_redisDatabase == null)
            {
                await _fallbackMemory.RemoveAsync(driverId, address, ct);
                return;
            }

            // -------------------------------------------------------------
            // StackExchange.Redis 物理集成伪代码示例如下（生产环境可直接取消注释并替换）：
            // -------------------------------------------------------------
            // string key = $"unicon:cache:{driverId}:{address}";
            // var db = (StackExchange.Redis.IDatabase)_redisDatabase;
            // await db.KeyDeleteAsync(key);

            await _fallbackMemory.RemoveAsync(driverId, address, ct);
        }
    }
}
