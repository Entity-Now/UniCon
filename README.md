# UniCon (Universal Connection)

[![Build Status](https://github.com/entity/UniCon/actions/workflows/dotnet.yml/badge.svg)](https://github.com/entity/UniCon/actions)
[![NuGet](https://img.shields.io/nuget/v/UniCon.Core.svg)](https://www.nuget.org/packages/UniCon.Core)
[![License](https://img.shields.io/github/license/entity/UniCon)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-blueviolet)](https://dotnet.microsoft.com/download/dotnet/10.0)

**UniCon** 是一款高性能、插件化的 .NET 工业物联网通讯集成框架。其核心目标是实现 **“协议抽象化”** 与 **“自愈自动化”**，通过统一的类 Web 契约屏蔽不同工业协议间的底层差异。

[English](./README_EN.md) | 简体中文

---

## 🚀 核心特性

- **🛡️ 结构化读写契约**: 借鉴 HTTP 设计思路，引入 `UniconRequest/Response` 模型，支持 Headers、Parameters 与 DataValue 包装。
- **🔄 自主自愈机制 (Watchdog)**: 内置 Watchdog 监控与指数退避重连算法，确保工业现场通讯的极高稳定性。
- **⚡ 高并发安全**: 驱动基类内置 `SemaphoreSlim` 并发控制，解决串行协议在高并发下的指令交织与冲突。
- **🕒 工业级任务调度**: 深度集成 **Quartz.NET**，支持 CRON 表达式、内置 HttpJob 与通讯联动 Job。
- **📊 质量追溯**: 数据自带 **质量戳 (Status)** 与 **源时间戳 (Timestamp)**，满足工业 4.0 的数据审计需求。
- **🧩 插件化驱动**: 模块化支持 S7, OPC UA, Modbus, MQTT，且易于扩展自定义协议。

---

## 🛠️ 支持协议

| 驱动 | 职责 | 底层依赖 |
| :--- | :--- | :--- |
| **Siemens S7** | 西门子全系列 PLC 通讯 | S7netplus |
| **OPC UA** | 高性能 OPC UA 客户端 | Workstation.UaClient |
| **Modbus** | 标准 Modbus TCP/RTU 读写 | EasyModbusTCP |
| **MQTT** | 轻量级消息队列发布订阅 | MQTTnet |

---

## 📦 快速开始

### 1. 安装驱动

```bash
dotnet add package UniCon.Drivers.S7
```

### 2. 基础读取案例

```csharp
using UniCon.Core;
using UniCon.Drivers.S7;

// 初始化驱动与自愈配置
var s7Driver = new S7Driver("PLC_01", logger);
s7Driver.EnableAutoReconnect = true;

// 建立物理连接
await s7Driver.ConnectAsync("CpuType=S71200;Ip=192.168.0.100;Rack=0;Slot=1");

// 执行结构化读取
var response = await s7Driver.ReadAsync<float>(new UniconRequest { Address = "DB1.DBD0" });

if (response.Success) {
    Console.WriteLine($"数据: {response.Data.Value} (质量: {response.Data.Status})");
}
```

---

## 🏗️ 架构概览

UniCon 采用分层解耦设计，确保核心业务逻辑不受底层协议变动的影响：

1. **UniCon.Core**: 核心契约、自愈逻辑、并发控制模型。
2. **UniCon.Drivers.***: 各协议的具体适配层。
3. **UniCon.WebServer**: 基于 ASP.NET Core 的管理与数据交互接口。
4. **Job System**: 驱动联动与外部集成的定时调度中心。

---

## 📚 文档

详细的开发文档与集成指南请参考：

- [1. 项目简介 (Introduction)](./docs/1.%20introduce/1.intro.md)
- [2. 集成指南 (Usage Guide)](./docs/1.%20introduce/2.usage.md)
- [3. 核心架构 (Architecture)](./docs/2.%20core/1.intro.md)
- [4. 任务调度 (Job System)](./docs/4.%20jobs/1.intro.md)

---

## 🤝 贡献

我们欢迎任何形式的贡献！请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 了解如何参与。

---

## 📄 开源协议

本项目基于 [MIT License](LICENSE) 协议开源。