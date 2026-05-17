using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Caching
{
    /// <summary>
    /// 内置高并发线程安全内存缓存提供者实现 (RULE 2.1)
    /// </summary>
    public class MemoryCacheProvider : IUniconCacheProvider
    {
        private readonly ConcurrentDictionary<(string DriverId, string Address), DataValue<object>> _cache = new();

        public Task<DataValue<object>?> GetAsync(string driverId, string address, CancellationToken ct = default)
        {
            if (_cache.TryGetValue((driverId, address), out var value))
            {
                return Task.FromResult<DataValue<object>?>(value);
            }
            return Task.FromResult<DataValue<object>?>(null);
        }

        public Task SetAsync(string driverId, string address, DataValue<object> dataValue, CancellationToken ct = default)
        {
            _cache[(driverId, address)] = dataValue;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string driverId, string address, CancellationToken ct = default)
        {
            _cache.TryRemove((driverId, address), out _);
            return Task.CompletedTask;
        }
    }
}
