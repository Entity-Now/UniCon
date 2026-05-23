---
url: /docs/index.md
---
# UniCon 项目简介

## 概述

UniCon（Universal Connection）是一款高性能、插件化的 .NET 工业物联网通讯集成框架，专为工业边缘网关、SCADA 平台及数字孪生系统设计，运行于 .NET 10+。

框架通过统一的南向驱动接口屏蔽各协议差异，提供完整的生命周期管理、自愈重连机制、主动采集订阅引擎及 REST API 北向接入，帮助开发者以极低的集成成本接入多种工业协议设备。

## 核心价值

* **协议抽象化**：通过统一的 `IUniconDriver` 接口、`UniconRequest` / `UniconResponse<T>` 请求响应模型，屏蔽 S7、Modbus、OPC UA、MQTT、OPC UA PubSub 等不同协议之间的差异。
* **连接自愈**：`DriverBase` 内置 Watchdog 自愈线程，支持网络中断感知（`INetworkMonitor`）与指数退避重连，断网挂起、上线即愈。
* **高性能扫描调度**：基于最小等待时间调度的 `ScanScheduler`，`Channel<T>` 异步解耦采集线程与回调线程，`IScanStrategy` 策略模式支持 `ExceptionBased`（死区过滤）与 `Polled` 两种模式。
* **对象-设备映射（ODM）**：类 ORM 设计，通过 `[UniconDevice]` 与 `[UniconAddress]` 特性将业务实体属性自动绑定到设备寄存器，免硬编码自动读写。
* **任务调度系统**：基于 Quartz.NET 深度封装，提供 `HttpJob`、`CommunicationJob` 等内置定时任务，支持运行时 CRUD 动态管理。
* **REST API 北向接入**：基于 ASP.NET Core Minimal API，提供驱动管理、数据读写、订阅控制、ODM 映射及任务调度全套接口。
* **可插拔缓存**：内置 `MemoryCacheProvider`（基于 `ConcurrentDictionary`），可无缝替换为 Redis 等分布式实现。

## 项目架构

```
UniCon
├── UniCon.Core              # 核心契约、调度引擎、ODM、缓存、任务
├── UniCon.Drivers.S7        # 西门子 S7 驱动（S7.Net）
├── UniCon.Drivers.Modbus    # Modbus TCP 驱动（EasyModbus）
├── UniCon.Drivers.OpcUa     # OPC UA 客户端驱动（Workstation.UaClient）
├── UniCon.Drivers.Mqtt      # MQTT 发布订阅驱动（MQTTnet）
├── UniCon.Drivers.OpcUaPubSub  # OPC UA PubSub UDP/MQTT 无连接驱动
└── UniCon.WebServer         # Minimal API 宿主（驱动管理、ODM、任务 API）
```

## 支持的工业协议

| 驱动 | 协议 / 标准 | 底层库 | 驱动别名 |
|------|------------|--------|---------|
| `S7Driver` | 西门子 S7（ISO-on-TCP）S7-200/300/1200/1500 | S7.Net | `"S7"` |
| `ModbusDriver` | Modbus TCP（线圈/保持/输入/离散寄存器） | EasyModbus | `"Modbus"` |
| `OpcUaDriver` | OPC UA Client/Server（匿名/账户/证书） | Workstation.UaClient | `"OpcUa"` |
| `MqttDriver` | MQTT 3.1/5.0 Pub/Sub（TLS 可选） | MQTTnet | `"Mqtt"` |
| `OpcUaPubSubDriver` | OPC UA PubSub UDP 组播 / MQTT（UADP/JSON） | 自研 | `"OpcUaPubSub"` |

## 目标人群

适用于需要集成多种工业设备（PLC、OPC UA Server、Modbus 仪表、MQTT Broker 等）进行数据采集、中转、分析或展示的开发者，尤其适合工业边缘网关、数字孪生平台与 IoT 集成系统场景。
