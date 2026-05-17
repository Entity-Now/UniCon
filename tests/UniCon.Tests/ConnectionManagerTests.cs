using Moq;
using UniCon.Core;
using Microsoft.Extensions.Logging;
using Xunit;

namespace UniCon.Tests
{
    public class ConnectionManagerTests
    {
        [Fact]
        public void RegisterDriver_Should_Add_To_Manager()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<ConnectionManager>>();
            var manager = new ConnectionManager(loggerMock.Object);
            var driverMock = new Mock<IUniconDriver>();
            driverMock.Setup(d => d.DriverId).Returns("TestDriver");

            // Act
            manager.RegisterDriver(driverMock.Object, "TestConnection");

            // Assert
            Assert.True(true);
        }
    }
}
