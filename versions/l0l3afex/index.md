---
url: /versions/l0l3afex/index.md
---
# UniCon 版本更新记录

## \[v1.2.0] - 2026-05-16

### 更新内容

* **任务管理系统重构 (Job Management)**:
  * 移除了基础的 `JobManager`，全面集成高性能调度框架 **Quartz.NET**。
  * 实现了 `JobScheduler` 包装类，支持 CRON 表达式调度。
  * 新增内置任务类型：
    * `HttpJob`: 支持自定义 URL 和 HTTP 方法的异步请求任务。
    * `SystemCleanupJob`: 预留的系统缓存与资源清理任务接口。
  * 支持通过依赖注入 (DI) 动态加载自定义任务类。
* **集成优化**:
  * 在 `UniCon.WebServer` 中配置了 Quartz 托管服务。
  * 引入了 `IHttpClientFactory` 优化 HTTP 任务的性能与资源管理。
* **测试增强**:
  * 增加了 `JobSystemTests`，通过 Mock 技术验证了新任务系统的执行逻辑。

### 与上一版本区别

* **功能性**: 从简单的 `while` 循环调度升级为支持 CRON 表达式、持久化与复杂调度逻辑的专业级系统。
* **扩展性**: 用户现在可以通过实现 `IJob` 接口或继承 `UniConJobBase` 轻松扩展自定义业务逻辑，而无需修改核心代码。
* **健壮性**: 利用 Quartz.NET 成熟的任务生命周期管理，提升了任务执行的可靠性与可监控性。

## \[v1.2.0 优化] - 2026-05-16

### 架构精简与规范化 (Refinement)

* **目录重构**: 将内置任务 (`HttpJob`, `SystemCleanupJob`) 迁移至 `BuiltIn` 子文件夹，每个任务独立文件，遵循 **单一职责原则 (RULE 2.1)**。
* **消除魔法字符串**: 引入 `JobDataKeys` 常量类，所有任务参数传递均使用常量定义，遵循 **语义化开发 (RULE 1.1)**。
* **代码拆分**: 将 `JobScheduler`、`UniConJobBase` 等核心组件从单一文件拆分为独立模块，提升了代码的可读性与维护性。
