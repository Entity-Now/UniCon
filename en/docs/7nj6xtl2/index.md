---
url: /en/docs/7nj6xtl2/index.md
---
# Job Scheduler

## Overview

`JobScheduler` is the core manager of the UniCon Job System. Highly encapsulated based on Quartz.NET, it aims to provide highly reliable, fine-grained cron job execution and management for southbound driver register interactions, northbound HTTP api triggers, and background system self-healing.

It resolves the following problems:

1. Provides complete scheduled task lifecycle management (CRUD).
2. Supports high-precision CRON triggers at the second level.
3. Tracks the count of currently active running tasks for system performance monitoring.
4. Seamlessly supports ASP.NET Core Dependency Injection (DI) and Minimal Web APIs.

## Usage

Complete container registration using the elegant one-click extension method `AddUniConJobs()` on `IServiceCollection`, then call it by injecting `JobScheduler` into your constructors.

```csharp
// 1. Inject in Program.cs
builder.Services.AddUniConJobs();

// 2. Inject and use via constructor
public class MyService
{
    private readonly JobScheduler _scheduler;
    public MyService(JobScheduler scheduler)
    {
        _scheduler = scheduler;
    }
}
```

## Parameters

### `ScheduleJobAsync<T>` Parameters

| Parameter | Type | Description | Required | Default Value |
|--------|------|------|----------|--------|
| jobId | `string` | Unique identifier ID of the job | Yes | - |
| cronExpression | `string` | Standard Quartz CRON trigger expression | Yes | - |
| data | `JobDataMap` | External parameter mapping accompanying the job execution | No | `null` |

### `UpdateJobAsync` Parameters

| Parameter | Type | Description | Required | Default Value |
|--------|------|------|----------|--------|
| jobId | `string` | Unique identifier ID of the job | Yes | - |
| cronExpression | `string` | New standard Quartz CRON trigger expression | Yes | - |
| data | `JobDataMap` | New external parameter mapping; updates if not null | No | `null` |

## Returns

### `GetExecutingJobsCountAsync` Returns

| Type | Description |
|------|------|
| `Task<int>` | Total number of jobs currently in the running state. |

### `GetJobsAsync` Returns

| Type | Description |
|------|------|
| `Task<List<JobInfo>>` | List of all scheduled job metadata and their execution states. |

### `UpdateJobAsync` / `DeleteJobAsync` Returns

| Type | Description |
|------|------|
| `Task<bool>` | Indicates whether the operation succeeded (returns `false` if the specified Job is not found). |

## Examples

**Example 1: Dynamically register and schedule an HttpJob**

```csharp
var jobData = new JobDataMap
{
    [JobDataKeys.HttpUrl] = "https://api.unicontroller.com/heartbeat",
    [JobDataKeys.HttpMethod] = "GET"
};

await jobScheduler.ScheduleJobAsync<HttpJob>(
    jobId: "ControllerHeartbeat",
    cronExpression: "0 0/5 * * * ?", // Execute every 5 minutes
    data: jobData
);
```

**Example 2: Query all jobs and print to console**

```csharp
var jobs = await jobScheduler.GetJobsAsync();
foreach (var job in jobs)
{
    Console.WriteLine($"Job: {job.JobId}, Type: {job.JobType}, Status: {job.Status}");
}
```

**Example 3: Update trigger time and parameters of an existing job**

```csharp
var newJobData = new JobDataMap
{
    [JobDataKeys.HttpUrl] = "https://api.unicontroller.com/v2/heartbeat",
    [JobDataKeys.HttpMethod] = "POST"
};

bool isUpdated = await jobScheduler.UpdateJobAsync(
    jobId: "ControllerHeartbeat",
    cronExpression: "0/30 * * * * ?", // Shortened to execute every 30 seconds
    data: newJobData
);
```
