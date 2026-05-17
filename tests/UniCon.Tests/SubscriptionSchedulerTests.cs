using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using UniCon.Core;
using UniCon.Core.Models;
using Xunit;

namespace UniCon.Tests
{
    public class TestDriver : DriverBase
    {
        public int ReadCallCount { get; private set; }
        public List<string> ReadAddresses { get; } = new();

        public TestDriver(string driverId, ILogger logger) : base(driverId, logger)
        {
        }

        public void SetConnected()
        {
            State = DriverState.Connected;
        }

        protected override Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
        {
            return Task.FromResult(true);
        }

        protected override Task OnDisconnectAsync(CancellationToken ct)
        {
            return Task.CompletedTask;
        }

        protected override Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
        {
            ReadCallCount++;
            lock (ReadAddresses)
            {
                ReadAddresses.Add(request.Address);
            }
            return Task.FromResult(UniconResponse<T>.CreateSuccess((T)(object)42));
        }

        protected override Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
        {
            return Task.FromResult(UniconResponse<bool>.CreateSuccess(true));
        }

        public override Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(IEnumerable<UniconRequest> requests, CancellationToken ct = default)
        {
            var reqList = requests.ToList();
            ReadCallCount++;
            lock (ReadAddresses)
            {
                ReadAddresses.AddRange(reqList.Select(r => r.Address));
            }
            return Task.FromResult<IEnumerable<UniconResponse<object>>>(
                reqList.Select(r => UniconResponse<object>.CreateSuccess((object)42))
            );
        }

        public override void Dispose()
        {
            CleanupSubscriptions();
        }
    }

    public class SubscriptionSchedulerTests
    {
        private readonly Mock<ILogger> _loggerMock = new();

        [Fact]
        public async Task Scheduler_GroupsAndBatchesReads_AndTriggersCallbacks()
        {
            // Arrange
            var driver = new TestDriver("TestDriver_01", _loggerMock.Object);
            driver.SetConnected();

            var callback1Called = false;
            var callback2Called = false;
            object val1 = null;
            object val2 = null;

            var sub1 = new UniconSubscription
            {
                Address = "DB1.DBD0",
                ScanRateMs = 10,
                ScanMode = UniconScanMode.Polled,
                Callback = data => { callback1Called = true; val1 = data.Value; }
            };

            var sub2 = new UniconSubscription
            {
                Address = "DB1.DBD4",
                ScanRateMs = 10,
                ScanMode = UniconScanMode.Polled,
                Callback = data => { callback2Called = true; val2 = data.Value; }
            };

            // Act
            var id1 = await driver.SubscribeAsync(sub1);
            var id2 = await driver.SubscribeAsync(sub2);

            // Verify subscriptions are in list
            var activeSubs = driver.GetSubscriptions().ToList();
            Assert.Contains(sub1, activeSubs);
            Assert.Contains(sub2, activeSubs);

            // Wait a short time for scheduler tick to run
            await Task.Delay(150);

            // Assert
            Assert.True(callback1Called);
            Assert.True(callback2Called);
            Assert.Equal(42, val1);
            Assert.Equal(42, val2);

            // Unsubscribe
            await driver.UnsubscribeByIdAsync(id1);
            await driver.UnsubscribeByIdAsync(id2);

            var activeSubsAfter = driver.GetSubscriptions().ToList();
            Assert.Empty(activeSubsAfter);

            driver.Dispose();
        }
    }
}
