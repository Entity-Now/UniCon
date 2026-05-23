---
url: /versions/6khy1hnx/index.md
---
# v2.1.0 — Job 调度系统功能增强与 IoC 封装

**日期**：2026-05-18
**类型**：Feature / Enhancement

***

## 变更概述

为了提升 UniCon 系统任务调度的灵活性与易用性，本次更新对 Job 模块进行了全面升级，包括：

1. **当前执行任务统计**：新增实时统计和获取当前正在执行的 Job 数量及上下文信息。
2. **完整的增删改查 (CRUD)**：支持对已调度任务进行添加、修改 (Reschedule / 更新 JobData)、删除和查询详情/列表。
3. **IoC 扩展集成 (AddUniConJobs)**：封装一键式 IoC 容器注入方法，极大降低了用户集成和使用 Job 模块的门槛。
4. **单元测试与 API 路由补全**：新增/更新对应的测试用例及 WebServer 的 API 路由，确保生产级稳定性。

***

## \[v2.1.0] - 2026-05-18

### Added / Changed / Fixed

* **Added**：新增 `JobInfo` 数据模型，用以优雅呈现当前任务的调度状态、参数配置及触发器类型。
* **Added**：在 `JobScheduler` 中实现了活动任务运行统计 `GetExecutingJobsCountAsync()`、获取全局任务列表 `GetJobsAsync()` 以及获取单个任务调度详情 `GetJobAsync(id)`。
* **Added**：实现了高灵活性的任务增删改查 (CRUD)，支持按静态强类型或运行时动态类型调度 `ScheduleJobAsync(...)`，按唯一 Key 删除任务 `DeleteJobAsync(id)`，以及动态更新 Cron 规则与伴随参数 `UpdateJobAsync(...)`。
* **Added**：在 `ServiceCollectionExtensions` 中设计了 `AddUniConJobs()` 极简 IoC 扩展，集成了 Quartz.NET 微软依赖注入工厂支持，并对当前程序集内所有非抽象的 `IJob` 实现类进行自动扫描和瞬时依赖项注入。
* **Added**：在 WebServer 项目中为 `Program.cs` 配套完成了 5 个极简 Web API 路由，用于监控和控制运行时的任务生命周期。
* **Added**：为测试项目 `JobSystemTests.cs` 配套补充了 4 个涵盖 CRUD 和统计的完整单元测试。
* **Added**：补充了组件架构和 API 设计文档于 `docs/4. jobs/` 目录。

### Key Changes

* 将原本繁琐的 Quartz 集成流程与内置任务注册封装合并为了极简的 `builder.Services.AddUniConJobs();` 一行式注入，对用户完全隐藏了底层复杂的配置逻辑。
* 动态 API 支持解析任意实现了 `IJob` 接口的任务，解决了传统框架下必须预先硬编码依赖注入导致的任务扩展受限瓶颈。
* 统一了运行时与调度器的生命周期托管，修复了可能导致上下文死锁的 Disposing 泄露。

\------details-----

## 🔍 Task Details

## 📌 Current Task Board: Job 调度系统升级

### 🎯 最终目标

* 完善 `JobScheduler` CRUD、当前执行数量统计，并在 `ServiceCollectionExtensions` 中提供一键 IoC 注入，补全单元测试与 API 路由。

### 📂 涉及文件 (Strictly Locked)

* \[x] src/UniCon.Core/Jobs/JobInfo.cs (新增 Job 详情信息模型)
* \[x] src/UniCon.Core/Jobs/JobScheduler.cs (完善 CRUD 及统计方法)
* \[x] src/UniCon.Core/ServiceCollectionExtensions.cs (增加 AddUniConJobs 的 IoC 扩展)
* \[x] tests/UniCon.Tests/JobSystemTests.cs (补充新功能的单元测试)
* \[x] src/UniCon.WebServer/Program.cs (更新 IoC 注入并添加 Job 相关的 API 路由)
* \[x] docs/4. jobs/1.intro.md (完善文档)

### 📝 Steps

* \[x] Step 1: 新建 `JobInfo.cs` 信息模型。
* \[x] Step 2: 完善 `JobScheduler.cs`，实现执行数量统计、获取 Job 列表/详情、动态调度、删除和更新 Job 的方法。
* \[x] Step 3: 在 `ServiceCollectionExtensions.cs` 中增加 `AddUniConJobs` 一键 IoC 注入方法。
* \[x] Step 4: 在 `JobSystemTests.cs` 中编写单元测试验证 Job 统计、CRUD 功能。
* \[x] Step 5: 优化 `Program.cs` 里的 IoC 注册，并增加 Job 的增删改查及统计 API 路由。
* \[x] Step 6: 编写/更新 `/docs/4. jobs` 相关文档，并执行质量门禁 (`dotnet build` & `dotnet test`)。

\------details end------
