using Moq;
using Quartz;
using UniCon.Core.Jobs;
using UniCon.Core.Jobs.BuiltIn;
using Microsoft.Extensions.Logging;
using Xunit;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

        [Fact]
        public async Task JobScheduler_Should_ScheduleJob_Correctly()
        {
            // Arrange
            var schedulerMock = new Mock<IScheduler>();
            var loggerMock = new Mock<ILogger<JobScheduler>>();
            var jobScheduler = new JobScheduler(schedulerMock.Object, loggerMock.Object);

            schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            await jobScheduler.ScheduleJobAsync<HttpJob>("test_job", "0/5 * * * * ?");

            // Assert
            schedulerMock.Verify(s => s.ScheduleJob(
                It.Is<IJobDetail>(j => j.Key.Name == "test_job"),
                It.Is<ITrigger>(t => t.Key.Name == "test_job_trigger"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task JobScheduler_Should_Return_ExecutingJobsCount()
        {
            // Arrange
            var schedulerMock = new Mock<IScheduler>();
            var loggerMock = new Mock<ILogger<JobScheduler>>();
            var jobScheduler = new JobScheduler(schedulerMock.Object, loggerMock.Object);

            var executingJobs = new List<IJobExecutionContext> { new Mock<IJobExecutionContext>().Object };
            schedulerMock.Setup(s => s.GetCurrentlyExecutingJobs(It.IsAny<CancellationToken>()))
                .ReturnsAsync(executingJobs.AsReadOnly());

            // Act
            var count = await jobScheduler.GetExecutingJobsCountAsync();

            // Assert
            Assert.Equal(1, count);
        }

        [Fact]
        public async Task JobScheduler_Should_DeleteJob_When_JobExists()
        {
            // Arrange
            var schedulerMock = new Mock<IScheduler>();
            var loggerMock = new Mock<ILogger<JobScheduler>>();
            var jobScheduler = new JobScheduler(schedulerMock.Object, loggerMock.Object);

            schedulerMock.Setup(s => s.CheckExists(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            schedulerMock.Setup(s => s.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await jobScheduler.DeleteJobAsync("test_job");

            // Assert
            Assert.True(result);
            schedulerMock.Verify(s => s.DeleteJob(It.Is<JobKey>(k => k.Name == "test_job"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task JobScheduler_Should_UpdateJob_Successfully()
        {
            // Arrange
            var schedulerMock = new Mock<IScheduler>();
            var loggerMock = new Mock<ILogger<JobScheduler>>();
            var jobScheduler = new JobScheduler(schedulerMock.Object, loggerMock.Object);

            var jobDetailMock = new Mock<IJobDetail>();
            var jobBuilder = JobBuilder.Create<HttpJob>().WithIdentity("test_job");
            jobDetailMock.Setup(j => j.GetJobBuilder()).Returns(jobBuilder);

            schedulerMock.Setup(s => s.GetJobDetail(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobDetailMock.Object);

            // Act
            var result = await jobScheduler.UpdateJobAsync("test_job", "0/10 * * * * ?", new JobDataMap());

            // Assert
            Assert.True(result);
            schedulerMock.Verify(s => s.AddJob(It.IsAny<IJobDetail>(), true, It.IsAny<CancellationToken>()), Times.Once);
            schedulerMock.Verify(s => s.RescheduleJob(
                It.Is<TriggerKey>(tk => tk.Name == "test_job_trigger"),
                It.Is<ITrigger>(t => t.Key.Name == "test_job_trigger"),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
