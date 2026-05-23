---
url: /docs/16mq4fzn/index.md
---
# 任务调度系统 (Job System)

## 概述

UniCon 任务调度系统是一个专为工业级边缘网关和高并发通讯设计的任务调度框架，完全基于 Quartz.NET 进行二次深度集成与封装。该系统完美支持 C# 10+ 异步编程模型，并为各种边缘设备数据定时上传、第三方 API 定时交互、后台通讯链路定时维护提供了开箱即用的基础设施支持。

## 模块职责与目标

* **物理点位与任务解耦**：允许用户通过任务编排以特定的时间频率自动定时读取或写入物理通道数据。
* **丰富的内置任务库**：提供 HTTP 请求、系统自愈清理、南向驱动实时读写等多场景任务模板。
* **动态生命周期控制 (CRUD)**：支持在运行时动态地对任务进行添加、修改、暂停、恢复和删除，且提供精准的活动任务执行数量计数。
* **零摩擦 IoC 集成**：提供一键注入扩展，自动扫描并注册所有 Job 实现，实现真正的极简接入。

## 包含的主要功能与文件

| 文件 / 功能组件 | 职责描述 |
|-----------------|----------|
| [1.intro.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/docs/4.%20jobs/1.intro.md) | 本文档，说明任务调度系统的职责、架构和包含的功能。 |
| [2.http-job.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/docs/4.%20jobs/2.http-job.md) | 定时进行外部 RESTful API 联动的高可靠 HTTP 通讯任务。 |
| [3.communication-job.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/docs/4.%20jobs/3.communication-job.md) | 定时对 UniCon 南向设备点位进行周期读写交互的任务。 |
| [4.job-scheduler.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/docs/4.%20jobs/4.job-scheduler.md) | 核心任务管理器 API 接口文档，包含 CRUD、状态统计及一键 DI 注入的使用指南。 |

## IoC 注入及初始化

系统提供了面向 `IServiceCollection` 的极其优雅的一键注入扩展方法 `AddUniConJobs()`：

```csharp
// 向 DI 容器注入调度系统，自动扫描并加载所有 IJob 任务
builder.Services.AddUniConJobs();

var app = builder.Build();

// 启动任务调度器
var scheduler = app.Services.GetRequiredService<JobScheduler>();
await scheduler.StartAsync();
```
