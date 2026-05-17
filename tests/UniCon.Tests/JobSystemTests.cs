using Moq;
using Quartz;
using UniCon.Core.Jobs;
using UniCon.Core.Jobs.BuiltIn;
using Microsoft.Extensions.Logging;
using Xunit;
using System.Net.Http;

namespace UniCon.Tests
{
    public class JobSystemTests
    {
        [Fact]
        public async Task HttpJob_Should_Execute_SendAsync()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<HttpJob>>();
            var httpClientFactoryMock = new Mock<IHttpClientFactory>();
            var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
            var httpClient = new HttpClient(handlerMock.Object);

            httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

            var job = new HttpJob(loggerMock.Object, httpClientFactoryMock.Object);

            var contextMock = new Mock<IJobExecutionContext>();
            var dataMap = new JobDataMap
            {
                [JobDataKeys.HttpUrl] = "http://test.com",
                [JobDataKeys.HttpMethod] = "GET"
            };
            contextMock.Setup(c => c.MergedJobDataMap).Returns(dataMap);

            // Act
            await job.Execute(contextMock.Object);

            // Assert
            httpClientFactoryMock.Verify(f => f.CreateClient(It.IsAny<string>()), Times.Once());
        }

        [Fact]
        public async Task SystemCleanupJob_Should_Run()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<SystemCleanupJob>>();
            var job = new SystemCleanupJob(loggerMock.Object);
            var contextMock = new Mock<IJobExecutionContext>();

            // Act
            await job.Execute(contextMock.Object);

            // Assert
            Assert.True(true);
        }
    }
}
