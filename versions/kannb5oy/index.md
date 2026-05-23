---
url: /versions/kannb5oy/index.md
---
# UniCon 版本更新记录

## \[v1.0.0] - 2026-05-16

### 更新内容

* **核心框架 (Core)**:
  * 定义了统一驱动接口 `IUniconDriver` 及基础抽象类 `DriverBase`。
  * 实现了 `ConnectionManager` 自动连接管理器，具备 5s 间隔的故障自愈与重连功能。
  * 实现了 `JobManager` 任务调度引擎，支持基于时间间隔的异步数据采集与处理。
  * 增加了 `NetworkHelper` 诊断工具，集成 Ping 和 TCP 端口连通性检查。
* **协议驱动 (Drivers)**:
  * **S7 Driver**: 基于 `S7netplus` 实现西门子 PLC 通讯。
  * **OPC UA Driver**: 基于 `Workstation.UaClient` 实现，支持匿名登录及标准读写。
  * **Modbus Driver**: 基于 `EasyModbus` 实现 TCP/RTU 寄存器读写。
  * **MQTT Driver**: 基于 `MQTTnet 5.x` 实现高性能发布订阅。
* **Web 服务 (Integration)**:
  * 搭建 `UniCon.WebServer` (ASP.NET Core 10) 中心化服务。
  * 提供 `CommunicationService` 作为业务枢纽，管理驱动、标签 (Tag) 与任务 (Job)。
  * 暴露 REST API 接口用于逻辑标签的读写访问。
  * 集成 Swagger UI 用于接口在线调试。

### 与上一版本区别

* **从 0 到 1 的飞跃**: 本版本为框架的首个完整实现版。
* **由虚转实**: 将《架构技术规格书》中的文字定义转化为可编译、可运行的 .NET 10 工业物联网框架。
* **插件化落地**: 建立了完整的驱动加载与管理模式，为后续扩展新协议奠定了基础。
