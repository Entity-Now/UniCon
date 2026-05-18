# UniCon (Universal Connection)

[![Build Status](https://github.com/entity/UniCon/actions/workflows/dotnet.yml/badge.svg)](https://github.com/entity/UniCon/actions)
[![NuGet](https://img.shields.io/nuget/v/UniCon.Core.svg)](https://www.nuget.org/packages/UniCon.Core)
[![License](https://img.shields.io/github/license/entity/UniCon)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/10.0)

**UniCon** 是一款高性能、插件化的 .NET 工业物联网通讯集成框架（基于 .NET 10）。其核心目标是实现 **“协议抽象化”**、**“自愈自动化”** 与 **“高并发实时调度”**，通过统一的类 Web 契约屏蔽不同工业协议间的底层差异，充当南向工业设备与北向 IT 系统之间的通用协议转换桥梁。

[English](./README_EN.md) | 简体中文

---

## 🚀 核心特性 (Key Features)

- **🛡️ 统一的读写契约**: 借鉴 HTTP 设计思路，引入 `UniconRequest/Response` 模型，支持结构化请求，底层驱动自动适配差异。
- **🔄 自主自愈机制 (Watchdog)**: 内置 Watchdog 监控与指数退避重连算法，确保工业现场通讯的极高稳定性，断网自动恢复。
- **⚡ v2 极速扫描引擎 (High-Concurrency)**: 全新基于 `Channel<T>` 的异步分发与无锁化设计，深度解耦 `ScanScheduler`、`ScanGroupRegistry` 与 `NotificationDispatcher`，在高频并发采集下实现极低资源开销。
- **🕒 工业级任务调度**: 深度集成 **Quartz.NET**，支持 CRON 表达式，轻松实现通讯联动与复杂批处理调度。
- **🌐 动态 WebAPI 集成**: 内置开箱即用的 Minimal API，支持运行时动态注册/注销驱动，提供统一的 RESTful 接口供外部系统读写与订阅设备点位。
- **📊 质量与时序追溯**: 核心领域模型 `DataValue<T>` 自带数据质量戳 (Quality) 与设备源时间戳 (Server Timestamp)，满足工业 4.0 审计与数据清洗需求。
- **🧩 插件化架构**: 严格遵循 Clean Architecture 与 DIP，提供 S7, OPC UA, Modbus, MQTT 等协议的模块化支持，实现自定义协议极其简单。

---

## 🛠️ 支持协议 (Supported Protocols)

| 驱动包 | 目标协议 / 设备 | 底层依赖 | 状态 |
| :--- | :--- | :--- | :--- |
| **`UniCon.Drivers.S7`** | 西门子全系列 PLC (S7-1200/1500/300) | S7netplus | ✅ Ready |
| **`UniCon.Drivers.OpcUa`** | 高性能 OPC UA 客户端 | Workstation.UaClient | ✅ Ready |
| **`UniCon.Drivers.Modbus`** | 标准 Modbus TCP/RTU 读写 | EasyModbusTCP | ✅ Ready |
| **`UniCon.Drivers.Mqtt`** | 轻量级消息队列发布订阅 | MQTTnet | ✅ Ready |
| **`UniCon.Drivers.OpcUaPubSub`** | OPC UA 组播订阅 (UDP) | - | 🚧 Beta |

---

## 📦 快速开始 (Quick Start)

### 1. 安装与引用

首先安装核心包与你需要的驱动协议包：

```bash
dotnet add package UniCon.Core
dotnet add package UniCon.Drivers.S7
```

### 2. ASP.NET Core 服务注册

在 `Program.cs` 中一键注入 UniCon 基础组件（包含缓存、连接管理器与注册中心）：

```csharp
// 注册 UniCon 核心依赖 (DI)
builder.Services.AddUniCon();

// 注册 Quartz 调度器 (可选)
builder.Services.AddQuartz();
builder.Services.AddQuartzHostedService();
```

### 3. 主动采集与订阅 (v2 调度引擎)

UniCon v2 引入了高并发的扫描调度引擎。您可以通过订阅模式，让框架自动完成后台轮询、死区计算及按需推送。

```csharp
using UniCon.Core;
using UniCon.Core.Models;
using UniCon.Drivers.S7;

// 1. 初始化驱动 (注入 CacheProvider)
var s7Driver = new S7Driver("PLC_01", logger, cacheProvider);
s7Driver.EnableAutoReconnect = true;

// 2. 建立连接
await s7Driver.ConnectAsync("CpuType=S71200;Ip=192.168.0.100;Rack=0;Slot=1");

// 3. 注册订阅
var subId = await s7Driver.SubscribeAsync(new UniconSubscription
{
    Address = "DB1.DBD0",
    ScanRateMs = 500, // 每 500ms 调度一次
    ScanMode = UniconScanMode.ExceptionBased, // 仅在数据变化或质量变化时触发回调
    Metadata = new TagMetadata { Deadband = 0.5, Unit = "℃" }, // 死区设置
    Callback = async dataValue => 
    {
        Console.WriteLine($"[{dataValue.ServerTimestamp}] 温度更新: {dataValue.Value} ℃ (质量: {dataValue.Quality})");
        await Task.CompletedTask;
    }
});
```

### 4. HTTP API 动态交互

使用集成的 WebAPI 扩展，您可以在外部通过 HTTP 直接读写：

```http
### 通过 RESTful 接口读取
GET http://localhost:5000/api/drivers/PLC_01/read?address=DB1.DBD0
```

---

## 🏗️ 架构概览 (Architecture)

UniCon 采用严格的分层解耦设计：

1. **`UniCon.Core`**: 领域核心，包含读写契约、并发控制、异常模型机制。
   - **扫描引擎层**: `ScanScheduler` (轮询调度), `ScanGroupRegistry` (地址去重合并), `NotificationDispatcher` (异步事件分发)。
2. **`UniCon.Drivers.*`**: 基础设施层，通过继承 `DriverBase` 并实现 `InternalReadAsync` 等抽象方法，快速接入各类第三方通信库。
3. **`UniCon.WebServer`**: 表现层，使用 Minimal API 提供统一的管理接口。
4. **`UniCon.Jobs`**: 应用层扩展，提供与设备状态联动的业务定时处理。

---

## 📚 文档库 (Documentation)

项目包含详尽的架构设计与集成说明，请参阅 `/docs` 目录：

- **[1. 介绍与指南 (Introduction & Usage)](./docs/1.%20introduce/1.intro.md)**
  - [快速入门与集成指南](./docs/1.%20introduce/2.usage.md)
- **[2. 核心架构 (Core Architecture)](./docs/2.%20core/1.intro.md)**
  - [驱动生命周期与注册中心](./docs/2.%20core/2.driver-registry.md)
  - [连接管理器](./docs/2.%20core/3.connection-manager.md)
  - [**v2 扫描调度引擎设计解析**](./docs/2.%20core/7.driver-architecture-v2.md)
- **[3. 驱动开发指南 (Drivers)](./docs/3.%20drivers/1.intro.md)**
- **[4. 任务调度中心 (Jobs)](./docs/4.%20jobs/1.intro.md)**
- **[5. WebAPI 集成 (WebAPI)](./docs/5.%20webapi/1.intro.md)**

---

## 🤝 贡献与反馈 (Contributing)

我们欢迎任何形式的贡献！
- 发现 Bug 或有新功能需求？请提交 [Issue](https://github.com/entity/UniCon/issues)。
- 准备提交代码？请遵守代码规范并提交 PR。

---

## 📄 开源协议 (License)

本项目基于 [MIT License](LICENSE) 协议开源。