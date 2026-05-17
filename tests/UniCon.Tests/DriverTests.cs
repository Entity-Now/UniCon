using Moq;
using Microsoft.Extensions.Logging;
using UniCon.Drivers.S7;
using UniCon.Drivers.Modbus;
using UniCon.Drivers.OpcUa;
using UniCon.Drivers.Mqtt;
using Xunit;

namespace UniCon.Tests
{
    public class DriverUsageTests
    {
        private readonly Mock<ILogger> _loggerMock = new();

        [Fact]
        public void S7Driver_Usage_Example()
        {
            var driver = new S7Driver("S7_01", _loggerMock.Object);
            // Example connection string: "CpuType=S71200;Ip=192.168.0.1;Rack=0;Slot=1"
            Assert.Equal("S7_01", driver.DriverId);
        }

        [Fact]
        public void ModbusDriver_Usage_Example()
        {
            var driver = new ModbusDriver("MB_01", _loggerMock.Object);
            // Example connection string: "Ip=127.0.0.1;Port=502"
            Assert.Equal("MB_01", driver.DriverId);
        }

        [Fact]
        public void OpcUaDriver_Usage_Example()
        {
            var driver = new OpcUaDriver("OPC_01", _loggerMock.Object);
            // Example connection string: "opc.tcp://localhost:4840"
            Assert.Equal("OPC_01", driver.DriverId);
        }

        [Fact]
        public void MqttDriver_Usage_Example()
        {
            var driver = new MqttDriver("MQTT_01", _loggerMock.Object);
            // Example connection string: "Server=broker.hivemq.com;ClientId=UniCon_Test"
            Assert.Equal("MQTT_01", driver.DriverId);
        }
    }
}
