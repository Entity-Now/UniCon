using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UniCon.Core
{
    /// <summary>
    /// 连接管理器接口契约 (DIP, 依赖倒置原则)
    /// </summary>
    public interface IConnectionManager : IDisposable
    {
        /// <summary>
        /// 注册并启动驱动。启动后驱动将由内置 Watchdog 自行维护连接。
        /// </summary>
        void RegisterDriver(IUniconDriver driver, string connectionString);

        /// <summary>
        /// 异步注册并激活驱动，带 CancellationToken
        /// </summary>
        Task RegisterDriverAsync(IUniconDriver driver, string connectionString, CancellationToken ct = default);

        /// <summary>
        /// 异步注销、断开并销毁指定的驱动实例
        /// </summary>
        Task UnregisterDriverAsync(string driverId, CancellationToken ct = default);

        /// <summary>
        /// 获取当前被接管的特定驱动实例
        /// </summary>
        IUniconDriver? GetDriver(string driverId);

        /// <summary>
        /// 获取所有当前连接管理器接管并运行中的驱动实例列表
        /// </summary>
        IEnumerable<IUniconDriver> GetManagedDrivers();
    }
}
