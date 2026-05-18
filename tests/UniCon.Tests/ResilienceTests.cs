using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Network;
using Xunit;

namespace UniCon.Tests
{
    public class ResilienceTestDriver : DriverBase
    {
        public int ConnectCount { get; private set; }
        public bool ConnectResult { get; set; } = true;

        public ResilienceTestDriver(string driverId, ILogger logger, IUniconCacheProvider cacheProvider, INetworkMonitor networkMonitor)
            : base(driverId, logger, cacheProvider, networkMonitor)
        {
        }

        public void TriggerFault()
        {
            State = DriverState.Faulted;
        }

        protected override Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            ConnectCount++;
            return Task.FromResult(ConnectResult);
        }

        protected override Task OnDisconnectAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        protected override Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
        {
            return Task.FromResult(UniconResponse<T>.CreateSuccess((T)(object)1));
        }

        protected override Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
        {
            return Task.FromResult(UniconResponse<bool>.CreateSuccess(true));
        }
    }

    public class ResilienceTests
    {
        private readonly Mock<ILogger> _loggerMock = new();
        private readonly IUniconCacheProvider _cache = new MemoryCacheProvider();

        [Fact]
        public async Task Driver_Should_PauseWatchdog_When_NetworkIsUnavailable()
        {
            // Arrange
            var networkMonitorMock = new Mock<INetworkMonitor>();
            networkMonitorMock.Setup(m => m.IsNetworkAvailable).Returns(false);

            var driver = new ResilienceTestDriver("ResDriver_01", _loggerMock.Object, _cache, networkMonitorMock.Object);

            // Connect to start watchdog (ConnectCount becomes 1)
            await driver.ConnectAsync("fake_connection_string");
            var initialConnectCount = driver.ConnectCount;

            // Act
            driver.TriggerFault(); // Transitioning to Faulted triggers Watchdog Loop

            // Let it run for a bit
            await Task.Delay(200);

            // Assert
            // ConnectCount should not increase beyond the initial connection because network is offline
            Assert.Equal(initialConnectCount, driver.ConnectCount);

            driver.Dispose();
        }

        [Fact]
        public async Task Driver_Should_TriggerWatchdogWakeup_When_NetworkRestored()
        {
            // Arrange
            var networkMonitorMock = new Mock<INetworkMonitor>();
            bool isAvailable = false;
            networkMonitorMock.Setup(m => m.IsNetworkAvailable).Returns(() => isAvailable);

            var driver = new ResilienceTestDriver("ResDriver_02", _loggerMock.Object, _cache, networkMonitorMock.Object);

            // Initial successful connection to start the watchdog
            await driver.ConnectAsync("fake_connection_string");
            var initialConnectCount = driver.ConnectCount;

            // Subsequent connection attempts in watchdog will fail
            driver.ConnectResult = false;
            driver.TriggerFault(); // Set State to Faulted to kick off watchdog reconnection attempts

            await Task.Delay(100);
            var countAfterFault = driver.ConnectCount;

            // Act - restore network and raise event
            isAvailable = true;
            networkMonitorMock.Raise(m => m.NetworkAvailabilityChanged += null, networkMonitorMock.Object, true);

            // Wait a short time for watchdog reactive wakeup
            await Task.Delay(150);

            // Assert
            // The connection attempts must increase because it was woken up immediately upon network availability event
            Assert.True(driver.ConnectCount > countAfterFault, "Watchdog should have been woken up and attempted reconnect after network restoration.");

            driver.Dispose();
        }
    }
}
