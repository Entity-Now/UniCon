using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Network;
using UniCon.Drivers.S7;
using UniCon.Drivers.Modbus;
using Xunit;

namespace UniCon.Tests
{
    public class DriverFactoryTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<INetworkMonitor> _networkMonitorMock = new();
        private readonly IUniconCacheProvider _cache = new MemoryCacheProvider();

        public DriverFactoryTests()
        {
            _networkMonitorMock.Setup(m => m.IsNetworkAvailable).Returns(true);

            var services = new ServiceCollection();

            // 注册通用系统与框架服务，模拟依赖注入容器
            services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
            services.AddSingleton<ILoggerFactory, LoggerFactory>();
            services.AddSingleton<IUniconCacheProvider>(_cache);
            services.AddSingleton<INetworkMonitor>(_networkMonitorMock.Object);

            // 注册统一的驱动注册与创建托管中心 (v2.3)
            services.AddSingleton<IDriverRegistry, DriverRegistry>();

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public void DriverRegistry_Should_ResolveFromContainer()
        {
            var registry = _serviceProvider.GetRequiredService<IDriverRegistry>();
            Assert.NotNull(registry);
        }

        [Fact]
        public void CreateDriverGeneric_Should_InstantiateAndAutowireDependencies()
        {
            var registry = _serviceProvider.GetRequiredService<IDriverRegistry>();

            // Act: 泛型自动依赖装配创建，完美提取并装配 logger, cache, networkMonitor
            var driver = registry.CreateDriver<S7Driver>("S7_Test_01");

            // Assert
            Assert.NotNull(driver);
            Assert.Equal("S7_Test_01", driver.DriverId);
            Assert.Equal(DriverState.Disconnected, driver.State);
        }

        [Fact]
        public void CreateDriverString_Should_InstantiateAndAutowireDependencies_AfterRegistration()
        {
            var registry = _serviceProvider.GetRequiredService<IDriverRegistry>();

            // Act: 自动扫描发现并注册带 [UniconDriver] 的驱动
            registry.DiscoverAndRegisterDrivers();

            // Act: 字符串别名动态创建
            var driverS7 = registry.CreateDriver("S7", "S7_Dyn_01");
            var driverModbus = registry.CreateDriver("Modbus", "MB_Dyn_01");

            // Assert
            Assert.NotNull(driverS7);
            Assert.Equal("S7_Dyn_01", driverS7.DriverId);
            Assert.IsType<S7Driver>(driverS7);

            Assert.NotNull(driverModbus);
            Assert.Equal("MB_Dyn_01", driverModbus.DriverId);
            Assert.IsType<ModbusDriver>(driverModbus);
        }

        [Fact]
        public void CreateDriverString_Should_ThrowException_WhenTypeNotRegistered()
        {
            var registry = _serviceProvider.GetRequiredService<IDriverRegistry>();

            // Act & Assert: 未配置别名简称应抛出不支持异常
            Assert.Throws<NotSupportedException>(() => registry.CreateDriver("NonExistentProtocol", "ID_Test_01"));
        }

        [Fact]
        public void DriverRegistry_Should_ManageActiveInstancesSuccessfully()
        {
            var registry = _serviceProvider.GetRequiredService<IDriverRegistry>();
            var driver = registry.CreateDriver<S7Driver>("PLC_Active_01");

            // Act: 注册托管活跃实例
            registry.Register(driver);
            var retrieved = registry.Get("PLC_Active_01");
            var all = registry.GetAll();

            // Assert: 验证生命周期检索
            Assert.NotNull(retrieved);
            Assert.Equal("PLC_Active_01", retrieved.DriverId);
            Assert.Contains(driver, all);

            // Act: 注销托管
            var unregistered = registry.Unregister("PLC_Active_01");
            var retrievedAfterUnregister = registry.Get("PLC_Active_01");

            // Assert: 验证注销状态
            Assert.True(unregistered);
            Assert.Null(retrievedAfterUnregister);
        }
    }
}
