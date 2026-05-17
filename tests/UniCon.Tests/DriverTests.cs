using Microsoft.Extensions.Logging;
using Moq;
using UniCon.Core.Caching;
using UniCon.Drivers.Modbus;
using UniCon.Drivers.Mqtt;
using UniCon.Drivers.OpcUa;
using UniCon.Drivers.S7;
using Xunit;

namespace UniCon.Tests
{
    public class DriverUsageTests
    {
        private readonly Mock<ILogger> _loggerMock = new();
        private readonly IUniconCacheProvider _cache = new MemoryCacheProvider();

        [Fact]
        public void S7Driver_Usage_Example()
        {
            var driver = new S7Driver("S7_01", _loggerMock.Object, _cache);
            Assert.Equal("S7_01", driver.DriverId);
        }

        [Fact]
        public void ModbusDriver_Usage_Example()
        {
            var driver = new ModbusDriver("MB_01", _loggerMock.Object, _cache);
            Assert.Equal("MB_01", driver.DriverId);
        }

        [Fact]
        public void OpcUaDriver_Usage_Example()
        {
            var driver = new OpcUaDriver("OPC_01", _loggerMock.Object, _cache);
            Assert.Equal("OPC_01", driver.DriverId);
        }

        [Fact]
        public void MqttDriver_Usage_Example()
        {
            var driver = new MqttDriver("MQTT_01", _loggerMock.Object, _cache);
            Assert.Equal("MQTT_01", driver.DriverId);
        }
    }
}
