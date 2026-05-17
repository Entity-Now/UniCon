using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace UniCon.Core
{
    /// <summary>
    /// 连接管理器：负责驱动的生命周期启动。
    /// 由于 DriverBase 已内置 Watchdog 自愈机制，本管理器不再执行轮询。
    /// </summary>
    public class ConnectionManager : IConnectionManager
    {
        private readonly ILogger<ConnectionManager> _logger;
        private readonly ConcurrentDictionary<string, IUniconDriver> _managedDrivers = new();

        public ConnectionManager(ILogger<ConnectionManager> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// 注册并启动驱动。启动后驱动将由内置 Watchdog 自行维护连接。
        /// </summary>
        public async void RegisterDriver(IUniconDriver driver, string connectionString)
        {
            await RegisterDriverAsync(driver, connectionString);
        }

        /// <summary>
        /// 异步注册并激活驱动，带 CancellationToken
        /// </summary>
        public async Task RegisterDriverAsync(IUniconDriver driver, string connectionString, CancellationToken ct = default)
        {
            if (_managedDrivers.TryAdd(driver.DriverId, driver))
            {
                _logger.LogInformation($"Starting driver {driver.DriverId} with auto-healing enabled.");
                try
                {
                    // 初始连接触发
                    await driver.ConnectAsync(connectionString, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Initial connection failed for {driver.DriverId}. Watchdog will take over.");
                }
            }
        }

        /// <summary>
        /// 异步注销、断开并销毁指定的驱动实例
        /// </summary>
        public async Task UnregisterDriverAsync(string driverId, CancellationToken ct = default)
        {
            if (_managedDrivers.TryRemove(driverId, out var driver))
            {
                _logger.LogInformation($"Stopping and unregistering driver {driverId}.");
                try
                {
                    await driver.DisconnectAsync(ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error disconnecting driver {driverId} during unregistration.");
                }
                finally
                {
                    driver.Dispose();
                }
            }
        }

        /// <summary>
        /// 获取被接管的单个驱动实例
        /// </summary>
        public IUniconDriver? GetDriver(string driverId)
        {
            return _managedDrivers.TryGetValue(driverId, out var driver) ? driver : null;
        }

        /// <summary>
        /// 获取所有连线管理器接管并运行中的驱动实例列表
        /// </summary>
        public IEnumerable<IUniconDriver> GetManagedDrivers()
        {
            return _managedDrivers.Values;
        }

        public void Dispose()
        {
            foreach (var driver in _managedDrivers.Values)
            {
                driver.Dispose();
            }
            _managedDrivers.Clear();
        }
    }
}
