using Quartz;
using Quartz.Impl.Matchers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
        /// 获取当前正在执行的任务数量
        /// </summary>
        public async Task<int> GetExecutingJobsCountAsync()
        {
            var executingJobs = await _scheduler.GetCurrentlyExecutingJobs();
            return executingJobs.Count;
        }

        /// <summary>
        /// 获取所有已调度任务列表
        /// </summary>
        public async Task<List<JobInfo>> GetJobsAsync()
        {
            var jobKeys = await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup());
            var jobs = new List<JobInfo>();

            foreach (var jobKey in jobKeys)
            {
                var jobDetail = await _scheduler.GetJobDetail(jobKey);
                if (jobDetail != null)
                {
                    var triggers = await _scheduler.GetTriggersOfJob(jobKey);
                    var trigger = triggers.FirstOrDefault();
                    var cronExpr = string.Empty;
                    var status = "Unknown";

                    if (trigger is ICronTrigger cronTrigger)
                    {
                        cronExpr = cronTrigger.CronExpressionString;
                    }

                    if (trigger != null)
                    {
                        var triggerState = await _scheduler.GetTriggerState(trigger.Key);
                        status = triggerState.ToString();
                    }

                    jobs.Add(new JobInfo(
                        jobKey.Name,
                        jobDetail.JobType.FullName ?? jobDetail.JobType.Name,
                        cronExpr,
                        status,
                        jobDetail.JobDataMap.ToDictionary(k => k.Key, v => v.Value)
                    ));
                }
            }

            return jobs;
        }

        /// <summary>
        /// 查询指定任务详细信息
        /// </summary>
        public async Task<JobInfo?> GetJobAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            var jobKey = new JobKey(jobId);
            var jobDetail = await _scheduler.GetJobDetail(jobKey);
            if (jobDetail == null)
            {
                return null;
            }

            var triggers = await _scheduler.GetTriggersOfJob(jobKey);
            var trigger = triggers.FirstOrDefault();
            var cronExpr = string.Empty;
            var status = "Unknown";

            if (trigger is ICronTrigger cronTrigger)
            {
                cronExpr = cronTrigger.CronExpressionString;
            }

            if (trigger != null)
            {
                var triggerState = await _scheduler.GetTriggerState(trigger.Key);
                status = triggerState.ToString();
            }

            return new JobInfo(
                jobKey.Name,
                jobDetail.JobType.FullName ?? jobDetail.JobType.Name,
                cronExpr,
                status,
                jobDetail.JobDataMap.ToDictionary(k => k.Key, v => v.Value)
            );
        }

        /// <summary>
        /// 安排一个任务 (泛型静态注册)
        /// </summary>
        public async Task ScheduleJobAsync<T>(string jobId, string cronExpression, JobDataMap? data = null) where T : IJob
        {
            await ScheduleJobAsync(jobId, typeof(T), cronExpression, data);
        }

        /// <summary>
        /// 安排一个任务 (动态类型注册)
        /// </summary>
        public async Task ScheduleJobAsync(string jobId, Type jobType, string cronExpression, JobDataMap? data = null)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                throw new ArgumentException("Cron expression cannot be null or empty.", nameof(cronExpression));
            }

            if (jobType == null)
            {
                throw new ArgumentNullException(nameof(jobType));
            }

            if (!typeof(IJob).IsAssignableFrom(jobType))
            {
                throw new ArgumentException($"Type {jobType.FullName} must implement {nameof(IJob)}", nameof(jobType));
            }

            var jobKey = new JobKey(jobId);
            if (await _scheduler.CheckExists(jobKey))
            {
                throw new InvalidOperationException($"Job '{jobId}' already exists.");
            }

            var job = JobBuilder.Create(jobType)
                .WithIdentity(jobKey)
                .SetJobData(data ?? new JobDataMap())
                .Build();

            var trigger = TriggerBuilder.Create()
                .WithIdentity($"{jobId}_trigger")
                .WithCronSchedule(cronExpression)
                .StartNow()
                .Build();

            await _scheduler.ScheduleJob(job, trigger);
            _logger.LogInformation($"Job {jobId} scheduled with type {jobType.Name} and CRON: {cronExpression}");
        }

        /// <summary>
        /// 删除任务
        /// </summary>
        public async Task<bool> DeleteJobAsync(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            var jobKey = new JobKey(jobId);
            if (!await _scheduler.CheckExists(jobKey))
            {
                return false;
            }

            var result = await _scheduler.DeleteJob(jobKey);
            if (result)
            {
                _logger.LogInformation($"Job {jobId} successfully deleted.");
            }
            return result;
        }

        /// <summary>
        /// 更新已安排的任务 (更新 Cron 与数据)
        /// </summary>
        public async Task<bool> UpdateJobAsync(string jobId, string cronExpression, JobDataMap? data = null)
        {
            if (string.IsNullOrWhiteSpace(jobId))
            {
                throw new ArgumentException("Job ID cannot be null or empty.", nameof(jobId));
            }

            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                throw new ArgumentException("Cron expression cannot be null or empty.", nameof(cronExpression));
            }

            var jobKey = new JobKey(jobId);
            var jobDetail = await _scheduler.GetJobDetail(jobKey);
            if (jobDetail == null)
            {
                return false;
            }

            if (data != null)
            {
                var updatedJob = jobDetail.GetJobBuilder()
                    .SetJobData(data)
                    .Build();
                await _scheduler.AddJob(updatedJob, replace: true);
            }

            var triggerKey = new TriggerKey($"{jobId}_trigger");
            var newTrigger = TriggerBuilder.Create()
                .WithIdentity(triggerKey)
                .WithCronSchedule(cronExpression)
                .StartNow()
                .Build();

            await _scheduler.RescheduleJob(triggerKey, newTrigger);
            _logger.LogInformation($"Job {jobId} updated successfully. New CRON: {cronExpression}");
            return true;
        }

        public async Task StopAsync()
        {
            await _scheduler.Shutdown();
        }

        public void Dispose()
        {
            try
            {
                _scheduler.Shutdown().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error shutting down scheduler during disposal");
            }
        }
    }
}
