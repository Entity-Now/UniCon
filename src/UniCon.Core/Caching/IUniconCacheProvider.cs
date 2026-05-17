using System.Threading;
using System.Threading.Tasks;
using UniCon.Core.Models;

namespace UniCon.Core.Caching
{
    /// <summary>
    /// UniCon 统一分布式/本地缓存提供者接口，规范工控网关的底层缓存介质 (RULE 2.1)
    /// </summary>
    public interface IUniconCacheProvider
    {
        /// <summary>
        /// 从底层缓存中异步获取指定驱动点位的最近一次物理采样值
        /// </summary>
        Task<DataValue<object>?> GetAsync(string driverId, string address, CancellationToken ct = default);

        /// <summary>
        /// 异步写入/刷新指定驱动点位的缓存采样数据与源时间戳/质量状态
        /// </summary>
        Task SetAsync(string driverId, string address, DataValue<object> dataValue, CancellationToken ct = default);

        /// <summary>
        /// 异步移除指定驱动点位的活跃缓存
        /// </summary>
        Task RemoveAsync(string driverId, string address, CancellationToken ct = default);
    }
}
