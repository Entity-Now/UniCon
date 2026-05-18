using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace UniCon.Core
{
    /// <summary>
    /// 驱动注册与创建中心：负责活跃驱动实例的托管生命周期，以及通过 DI 自动装配动态扫描创建驱动 (v2.3)
    /// </summary>
    public interface IDriverRegistry
    {
        // ─── 活跃实例生命周期管理 (Registry API) ───

        /// <summary>
        /// 将运行中的物理驱动实例注册并托管在生命周期中心
        /// </summary>
        void Register(IUniconDriver driver);

        /// <summary>
        /// 从托管中心注销指定的驱动实例
        /// </summary>
        bool Unregister(string driverId);

        /// <summary>
        /// 获取当前已托管的驱动活跃实例
        /// </summary>
        IUniconDriver? Get(string driverId);

        /// <summary>
        /// 获取托管中心中所有活跃运行驱动的只读列表
        /// </summary>
        IEnumerable<IUniconDriver> GetAll();

        // ─── 驱动别名配置与装配工厂 (Factory API) ───

        /// <summary>
        /// 注册驱动类型的别名简称到具体物理实现类（如 "S7" -> typeof(S7Driver)）
        /// </summary>
        void RegisterDriverType(string driverType, Type implementationType);

        /// <summary>
        /// 基于别名简称动态实例化驱动，其底层物理依赖（如 CacheProvider, NetworkMonitor）自适应通过 DI 装配
        /// </summary>
        IUniconDriver CreateDriver(string driverType, string driverId);

        /// <summary>
        /// 强类型泛型直接创建并装配驱动，无需提前注册别名简称
        /// </summary>
        T CreateDriver<T>(string driverId) where T : class, IUniconDriver;

        /// <summary>
        /// 自动扫描并加载当前运行域及运行目录下的所有带有 [UniconDriver] 特性的驱动实现类，进行自动装配与零配置注册 (Plug-and-Play)
        /// </summary>
        void DiscoverAndRegisterDrivers();
    }

    /// <summary>
    /// 驱动注册与创建中心的默认实现
    /// </summary>
    public class DriverRegistry : IDriverRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILoggerFactory _loggerFactory;

        // 运行中活跃物理驱动实例映射 (DriverId -> Driver)
        private readonly ConcurrentDictionary<string, IUniconDriver> _drivers = new(StringComparer.OrdinalIgnoreCase);

        // 协议简称别名与实现类 Runtime 类型映射 (DriverType -> Type)
        private readonly ConcurrentDictionary<string, Type> _typeRegistry = new(StringComparer.OrdinalIgnoreCase);

        public DriverRegistry(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        }

        // =========================================================================
        // 运行实例生命周期管理 (Registry Implementation)
        // =========================================================================

        public void Register(IUniconDriver driver)
        {
            ArgumentNullException.ThrowIfNull(driver);
            if (string.IsNullOrWhiteSpace(driver.DriverId))
                throw new ArgumentException("Driver ID cannot be empty during registration.", nameof(driver));

            _drivers[driver.DriverId] = driver;
        }

        public bool Unregister(string driverId)
        {
            if (string.IsNullOrWhiteSpace(driverId)) return false;
            return _drivers.TryRemove(driverId, out _);
        }

        public IUniconDriver? Get(string driverId)
        {
            if (string.IsNullOrWhiteSpace(driverId)) return null;
            return _drivers.TryGetValue(driverId, out var driver) ? driver : null;
        }

        public IEnumerable<IUniconDriver> GetAll()
        {
            return _drivers.Values;
        }

        // =========================================================================
        // 驱动自动依赖装配工厂 (Factory Implementation)
        // =========================================================================

        public void RegisterDriverType(string driverType, Type implementationType)
        {
            if (string.IsNullOrWhiteSpace(driverType))
                throw new ArgumentException("Driver type name cannot be empty.", nameof(driverType));

            ArgumentNullException.ThrowIfNull(implementationType);

            if (!typeof(IUniconDriver).IsAssignableFrom(implementationType))
            {
                throw new ArgumentException($"Type '{implementationType.FullName}' must implement '{nameof(IUniconDriver)}'.", nameof(implementationType));
            }

            _typeRegistry[driverType] = implementationType;
        }

        public IUniconDriver CreateDriver(string driverType, string driverId)
        {
            if (string.IsNullOrWhiteSpace(driverType))
                throw new ArgumentException("Driver type cannot be empty.", nameof(driverType));

            if (string.IsNullOrWhiteSpace(driverId))
                throw new ArgumentException("Driver ID cannot be empty.", nameof(driverId));

            if (!_typeRegistry.TryGetValue(driverType, out var implementationType))
            {
                throw new NotSupportedException($"Driver type '{driverType}' is currently not registered or supported in the system.");
            }

            var logger = _loggerFactory.CreateLogger(implementationType);

            // ActivatorUtilities 将会从 IServiceProvider 容器拉取 CacheProvider, NetworkMonitor 等环境依赖并完成装配
            // driverId 与 logger 会作为局部指定的位置参数强制配对
            return (IUniconDriver)ActivatorUtilities.CreateInstance(_serviceProvider, implementationType, driverId, logger);
        }

        public T CreateDriver<T>(string driverId) where T : class, IUniconDriver
        {
            if (string.IsNullOrWhiteSpace(driverId))
                throw new ArgumentException("Driver ID cannot be empty.", nameof(driverId));

            var logger = _loggerFactory.CreateLogger<T>();

            // 泛型强类型自动装配创建
            return ActivatorUtilities.CreateInstance<T>(_serviceProvider, driverId, logger);
        }

        public void DiscoverAndRegisterDrivers()
        {
            // 1. 扫描当前应用域中所有已加载的程序集
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in loadedAssemblies)
            {
                RegisterFromAssembly(assembly);
            }

            // 2. 扫描物理执行目录下所有的 "UniCon.Drivers.*.dll"
            // 主动加载以使驱动程序集对 AppDomain 可见，实现零配置的 Plug-and-Play
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(baseDirectory))
            {
                var files = Directory.GetFiles(baseDirectory, "UniCon.Drivers.*.dll");
                foreach (var file in files)
                {
                    try
                    {
                        var assemblyName = AssemblyName.GetAssemblyName(file);
                        // 避免重复加载
                        if (!Array.Exists(loadedAssemblies, a => a.GetName().Name == assemblyName.Name))
                        {
                            var loaded = Assembly.Load(assemblyName);
                            RegisterFromAssembly(loaded);
                        }
                    }
                    catch
                    {
                        // 忽略某些非受控 DLL 加载异常，保证框架主流程高可用
                    }
                }
            }
        }

        private void RegisterFromAssembly(Assembly assembly)
        {
            try
            {
                var types = assembly.GetTypes();
                foreach (var type in types)
                {
                    // 必须是非抽象的实现 IUniconDriver 的 class，且被 [UniconDriver] 标记
                    if (typeof(IUniconDriver).IsAssignableFrom(type) && !type.IsAbstract && type.IsClass)
                    {
                        var attribute = type.GetCustomAttribute<UniconDriverAttribute>();
                        if (attribute != null)
                        {
                            RegisterDriverType(attribute.DriverType, type);
                        }
                    }
                }
            }
            catch
            {
                // 忽略特定的反射加载类型异常
            }
        }
    }
}
