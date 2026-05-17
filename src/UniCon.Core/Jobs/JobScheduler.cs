using Quartz;
using Microsoft.Extensions.Logging;

namespace UniCon.Core.Jobs
{
    /// <summary>
    /// 任务管理器，封装 Quartz.NET
    /// </summary>
    public class JobScheduler : IDisposable
    {
        private readonly IScheduler _scheduler;
        private readonly ILogger<JobScheduler> _logger;

        public JobScheduler(IScheduler scheduler, ILogger<JobScheduler> logger)
        {
            _scheduler = scheduler;
            _logger = logger;
        }

        public async Task StartAsync()
        {
            await _scheduler.Start();
            _logger.LogInformation("JobScheduler started.");
        }

        /// <summary>
        /// 安排一个任务 (RULE 3.2 依赖于抽象 IJob)
        /// </summary>
        public async Task ScheduleJobAsync<T>(string jobId, string cronExpression, JobDataMap? data = null) where T : IJob
        {
            var job = JobBuilder.Create<T>()
                .WithIdentity(jobId)
                .SetJobData(data ?? new JobDataMap())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobId}_trigger")
                .WithCronSchedule(cronExpression)
                .StartNow()
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
            _logger.LogInformation($"Job {jobId} scheduled with CRON: {cronExpression}");
        }

        public async Task StopAsync()
        {
            await _scheduler.Shutdown();
        }

        public void Dispose()
        {
            StopAsync().Wait();
        }
    }
}
