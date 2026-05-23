<div align="center">
  <img src="https://img.icons8.com/color/120/000000/iot-sensor.png" alt="UniCon Logo" width="120" style="margin-bottom: 20px;"/>
  <h1>UniCon (Universal Connection)</h1>
  <p><b>🚀 高性能、插件化的 .NET 10 工业物联网通讯集成框架</b></p>
  <p><i>A High-Performance, Pluggable Industrial IoT Integration Framework Built on .NET 10</i></p>

  <p align="center">
    <a href="https://github.com/entity/UniCon/actions/workflows/dotnet.yml"><img src="https://github.com/entity/UniCon/actions/workflows/dotnet.yml/badge.svg" alt="Build Status"/></a>
    <a href="https://www.nuget.org/packages/UniCon.Core"><img src="https://img.shields.io/nuget/v/UniCon.Core.svg?style=flat-square&logo=nuget&color=004880" alt="NuGet"/></a>
    <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=.net" alt=".NET 10"/></a>
    <a href="LICENSE"><img src="https://img.shields.io/github/license/entity/UniCon?style=flat-square&color=44CC11" alt="License"/></a>
    <a href="https://github.com/entity/UniCon/stargazers"><img src="https://img.shields.io/github/stars/entity/UniCon?style=flat-square&color=F5C211" alt="GitHub Stars"/></a>
  </p>

  <p align="center">
    <b>简体中文</b> | <a href="./README_EN.md">English</a>
  </p>
</div>

---

## 📖 1. 项目简介

**UniCon (Universal Connection)** 是一款专为工业 4.0 与物联网场景设计的 **高性能、插件化 .NET 工业通讯集成框架**（基于最新的 **.NET 10** 构建）。

其核心使命在于实现 **“协议抽象化”**、**“自愈自动化”** 与 **“高并发实时调度”**。通过统一的类 Web 读写契约（HTTP Request/Response 模式），彻底屏蔽不同工业协议（如 S7, OPC UA, Modbus, MQTT）底层的复杂差异，充当南向工业设备与北向 IT 系统（MES/ERP/SCADA）之间的通用协议转换与高频数据同步桥梁。

无论是简单的点位读写，还是数万点位的高频实时轮询，UniCon 都能以极低的资源占用和极高的稳定性保驾护航。

---

## 🌟 2. 项目亮点与核心优势

- **🛡️ 统一读写契约 (Unified Protocol Abstraction)**  
  借鉴现代 Web 架构，引入 `UniconRequest/Response` 抽象模型。上层应用无需关注底层物理链路与协议格式，通过统一接口即可对 PLC、时序数据库或云端队列进行透明操作。
  
- **⚡ v2 极速扫描引擎 (High-Concurrency Scan Engine)**  
  全面重构的 v2 引擎基于 `System.Threading.Channels` 的异步分发与无锁化队列设计。彻底解耦了 `ScanScheduler`（轮询调度）、`ScanGroupRegistry`（地址合并去重）与 `NotificationDispatcher`（异步回调推送），在万级点位的高频并发采集下实现极低的 CPU 与内存开销。
  
- **🔄 自主自愈机制 (Smart Watchdog & Exponential Backoff)**  
  内置工业级 Watchdog 监控和指数退避重连算法。能够实时监测网络断线、设备宕机、链路拥堵并自动进行优雅重连，确保在恶劣的现场工业网环境下仍能实现通讯自愈。

- **🕒 工业级任务调度 (Enterprise Job Scheduling)**  
  深度集成 **Quartz.NET** 调度中心，支持高灵活度的 CRON 表达式。让点位采集、批处理、设备控制、心跳检测与业务任务协同调度变得轻而易举。

- **🌐 动态 WebAPI 扩展 (Dynamic Minimal REST API)**  
  内置开箱即用的 Minimal API 控制层，支持在运行时动态注册、注销、配置和健康检查各类通信驱动，提供标准 RESTful 接口供第三方系统直接调用。

- **📊 时序质量审计 (Data Quality & Server Timestamp)**  
  核心领域模型 `DataValue<T>` 自带数据质量戳 (`Quality`) 和设备源时间戳 (`Server Timestamp`)，完美适配时序数据库（InfluxDB/TDengine）对数据源头审计和异常清洗的要求。

- **🧩 强内聚插件化架构 (Clean Architecture & DIP)**  
  严格遵循 **Clean Architecture (清洁架构)** 与 **DIP (依赖倒置原则)**。提供高度解耦的 `DriverBase` 基类，新增通信协议驱动只需关注核心读写逻辑，即可无缝融入框架生态。

---

## 🛠️ 3. 技术栈清单

### 💻 核心技术与框架
* **运行时**: `.NET 10.0 (C# 14/15)`
* **依赖注入/Web**: `Microsoft.Extensions.DependencyInjection`, `ASP.NET Core Minimal APIs`
* **高并发核心**: `System.Threading.Channels`, `System.Threading.Tasks`
* **定时任务**: `Quartz.NET` (带有依赖注入支持)
* **规则验证**: `FluentValidation`
* **配置管理**: `Options Pattern` (配置选项模型)

### 🔌 支持协议与底层依赖驱动

| 驱动包 | 目标协议 / 设备 | 底层核心驱动库 | 状态 |
| :--- | :--- | :--- | :--- |
| **`UniCon.Drivers.S7`** | 西门子全系列 PLC (S7-1200/1500/300) | `S7netplus` | ✅ 生产级就绪 |
| **`UniCon.Drivers.OpcUa`** | 高性能 OPC UA 客户端 | `Workstation.UaClient` | ✅ 生产级就绪 |
| **`UniCon.Drivers.Modbus`** | 标准 Modbus TCP / RTU 读写 | `EasyModbusTCP` | ✅ 生产级就绪 |
| **`UniCon.Drivers.Mqtt`** | 轻量级消息队列发布订阅 | `MQTTnet` | ✅ 生产级就绪 |
| **`UniCon.Drivers.OpcUaPubSub`** | OPC UA 组播订阅 (UDP) | 自研 PubSub 解析器 | 🚧 灰度测试中 |

---

## 🚀 4. 环境要求与安装启动步骤

### 📋 4.1 开发环境要求
* **操作系统**: Windows 10/11, macOS Sequoia+, Linux (Ubuntu/CentOS)
* **SDK / 运行时**: [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) 或更高版本
* **IDE 工具**: Visual Studio 2022 (v17.12+) / JetBrains Rider 2025.1+ / VS Code (.NET C# Dev Kit)

### 📥 4.2 还原与构建

首先，克隆仓库到本地：

```bash
git clone https://github.com/entity/UniCon.git
cd UniCon
```

使用 .NET CLI 一键还原依赖并执行构建：

```bash
# 还原所有 NuGet 依赖包
dotnet restore

# 编译整个解决方案 (Release 模式)
dotnet build -c Release
```

### 🧪 4.3 运行单元测试

UniCon 拥有完善的单元测试覆盖，确保调度器、驱动管理器与 Channel 分发器的功能稳定性：

```bash
dotnet test
```

### ⚡ 4.4 启动 WebServer 演示网关

框架内置了一个开箱即用的 Minimal API Web 服务，可用于动态注入驱动和 RESTful 通讯交互：

```bash
# 启动表现层 Web API 网关
dotnet run --project src/UniCon.WebServer
```

服务启动后，可以通过浏览器访问控制台控制台或 Swagger 文档：
* **API 地址**: `http://localhost:5000` 或 `https://localhost:5001`
* **健康检查**: `http://localhost:5000/health`

---

## 💡 5. 基础使用代码示例

### 5.1 安装 NuGet 依赖包

UniCon 采用**插件化与模块化**的设计理念。核心引擎与各物理设备驱动包均已在 NuGet 独立发布：

> [!TIP]
> **💡 我需要依次安装所有的包吗？**
> **不需要。** 您**必须**且只需安装 `UniCon.Core`（核心协议抽象与调度引擎），然后**根据实际项目的物理设备需求，仅选择性地安装**对应的驱动包即可。例如：如果您的现场只有西门子 PLC，则仅需安装 `UniCon.Core` 和 `UniCon.Drivers.S7`。无需引入其他不需要的底层依赖（如 OPC UA 或 MQTT），从而保持项目轻量与纯净。

#### 📦 核心包 (必选)
* **.NET CLI**: `dotnet add package UniCon.Core`
* **Package Manager**: `Install-Package UniCon.Core`

#### 🔌 协议驱动包 (按需选择)

| 物理驱动包 | 目标协议 / 设备 | .NET CLI 安装指令 | Package Manager 控制台指令 |
| :--- | :--- | :--- | :--- |
| **`UniCon.Drivers.S7`** | 西门子全系列 PLC (S7-1200/1500/300) | `dotnet add package UniCon.Drivers.S7` | `Install-Package UniCon.Drivers.S7` |
| **`UniCon.Drivers.OpcUa`** | 高性能 OPC UA 客户端 | `dotnet add package UniCon.Drivers.OpcUa` | `Install-Package UniCon.Drivers.OpcUa` |
| **`UniCon.Drivers.Modbus`** | 标准 Modbus TCP / RTU | `dotnet add package UniCon.Drivers.Modbus` | `Install-Package UniCon.Drivers.Modbus` |
| **`UniCon.Drivers.Mqtt`** | MQTT 消息队列发布订阅 | `dotnet add package UniCon.Drivers.Mqtt` | `Install-Package UniCon.Drivers.Mqtt` |
| **`UniCon.OpcUaPubSub.Binary`** | OPC UA 组播二进制解析器 | `dotnet add package UniCon.OpcUaPubSub.Binary` | `Install-Package UniCon.OpcUaPubSub.Binary` |
| **`UniCon.OpcUaPubSub.Client`** | OPC UA 组播订阅客户端 | `dotnet add package UniCon.OpcUaPubSub.Client` | `Install-Package UniCon.OpcUaPubSub.Client` |


### 5.2 依赖注入配置 (Program.cs)

在一键配置框架基础层后，即可以构造函数方式注入 `IDriverRegistry` 和 `IConnectionManager`：

```csharp
using UniCon.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// 一键注册 UniCon 核心依赖（包含缓冲服务、连接生命周期管理、动态驱动注册中心）
builder.Services.AddUniCon();

// 注册 Quartz 定时调度（可选）
builder.Services.AddQuartzService(); 

var app = builder.Build();
app.Run();
```

### 5.3 异步高频数据采集与变化订阅 (v2 调度引擎)

以下展示了如何通过 UniCon 的 v2 扫描引擎，优雅地向西门子 S7-1200 PLC 建立连接、配置防抖死区、并实现基于异常（变化）推送的高频轮询。

```csharp
using UniCon.Core;
using UniCon.Core.Models;
using UniCon.Drivers.S7;
using Microsoft.Extensions.Logging;

// 1. 实例化西门子 S7 通讯驱动 (指定唯一设备标识)
ILogger<S7Driver> logger = loggerFactory.CreateLogger<S7Driver>();
var s7Driver = new S7Driver("Siemens_PLC_01", logger, cacheProvider);

// 开启自愈守卫: 自动处理网络中断与物理链路重连
s7Driver.EnableAutoReconnect = true;

// 2. 连接到 PLC 终端
await s7Driver.ConnectAsync("CpuType=S71200;Ip=192.168.1.200;Rack=0;Slot=1");

// 3. 注册变化订阅器 (基于 Channel 的无锁轮询调度)
var subscriptionId = await s7Driver.SubscribeAsync(new UniconSubscription
{
    Address = "DB10.DBD20",                 // PLC 点位寄存器地址
    ScanRateMs = 200,                       // 轮询周期: 200ms 快速扫描
    ScanMode = UniconScanMode.ExceptionBased,// 变化推送模式: 仅当值或 Quality 变化时触发
    Metadata = new TagMetadata 
    { 
        Deadband = 0.2,                     // 变化死区: 波动小于 0.2 时忽略推送，过滤噪声
        Unit = "Mpa"                        // 点位物理单位
    },
    Callback = async dataValue => 
    {
        // 收到过滤并校验后的安全数据值
        Console.WriteLine($"⏱️ [{dataValue.ServerTimestamp}] 压力变送器数据变化!");
        Console.WriteLine($"   💡 当前值: {dataValue.Value} {dataValue.Unit} (状态: {dataValue.Quality})");
        await Task.CompletedTask;
    }
});
```

### 5.4 统一 RESTful API 动态交互

您还可以通过标准的 HTTP REST 协议，在任何第三方语言（Java/Python/Go/Frontend）中轻松控制设备：

```http
### 请求：通过 UniCon 统一网关读取指定驱动的物理点位
GET http://localhost:5000/api/drivers/Siemens_PLC_01/read?address=DB10.DBD20
Accept: application/json

### 响应：统一的 UniconResponse 协议规范
{
  "success": true,
  "code": 200,
  "data": {
    "address": "DB10.DBD20",
    "value": 12.84,
    "quality": "Good",
    "serverTimestamp": "2026-05-23T17:24:28.104Z",
    "message": null
  }
}
```

---

## 📂 6. 项目文件目录说明

```text
UniCon/
├── .github/                  # CI/CD 自动化工作流目录
│   └── workflows/            # GitHub Actions (单元测试与文档部署)
├── .vuepress_docs/           # 基于 VuePress 2 构建的静态在线文档工程
│   ├── docs/                 # 系统架构、驱动开发等详尽文档目录
│   └── package.json          # 前端文档依赖管理
├── src/                      # 核心源码目录
│   ├── UniCon.Core/          # 💎 核心抽象与领域模型 (DIP、Channel 并发扫描引擎、契约定义)
│   ├── UniCon.Drivers.S7/    # 🔌 Siemens S7 协议驱动实现
│   ├── UniCon.Drivers.OpcUa/ # 🔌 OPC UA 客户端通讯实现
│   ├── UniCon.Drivers.Modbus/# 🔌 Modbus TCP / RTU 通讯实现
│   ├── UniCon.Drivers.Mqtt/  # 🔌 MQTT 轻量级发布订阅实现
│   ├── UniCon.WebServer/     # 🌐 Minimal Web API 宿主网关与动态管理接口
│   └── UniCon.Jobs/          # 🕒 Quartz 工业级集成任务扩展
├── tests/                    # 测试套件目录
│   └── UniCon.Tests/         # 🧪 针对扫描调度、无锁 Channel 的全套单元测试
├── UniCon.slnx               # .NET 10 新版轻量化解决方案声明文件
└── README.md                 # 项目主说明文档 (本文件)
```

---

## 🤝 7. 参与开发贡献方式

我们对所有的 Pull Request 和贡献都持非常开放与欢迎的态度！如果您想为 UniCon 添砖加瓦，请遵循以下流程：

### 🛠️ 7.1 开发工作流
1. **Fork** 本仓库到您的个人 GitHub 账号下。
2. 创建您的专属功能分支（Feature Branch）：
   ```bash
   git checkout -b feature/amazing-driver
   ```
3. 遵循本项目的 **代码质量标准**：
   * **复杂度控制**: 单个方法尽量保持在 40 行以内，避免嵌套过深。
   * **异步优先**: 所有 I/O 操作、通讯处理必须使用 `async / await` 结构。
   * **可观测性**: 关键步骤及异常捕获处引入 `ILogger` 进行结构化日志记录。
4. 在提交前对代码进行规范化格式化并确保所有测试通过：
   ```bash
   dotnet format
   dotnet test
   ```
5. 提交并推送您的修改，发起一个 **Pull Request** 到主仓库。

### 🐞 7.2 报告问题与反馈
如果您在现场使用或开发测试中遇到任何问题，请随时通过 [GitHub Issues](https://github.com/entity/UniCon/issues) 进行反馈。我们在提交 Issue 时建议附带：
- 运行的环境（如操作系统、.NET 具体版本）。
- 异常通讯驱动类型与异常堆栈日志。
- 可复现的最小代码片段。

---

## 📄 8. 开源协议与版权声明

本项目基于 **[MIT License](LICENSE)** 协议开源。这意味着您可以免费在商用及私人项目中自由使用、修改及分发本框架源码，但必须在分发版本中包含原作者的版权声明与许可声明。

---

<div align="center">
  <p><b>UniCon — 让工业互联更简单、更稳健、更高效。</b></p>
  <p>如果您觉得这个项目对您有所帮助，欢迎在 GitHub 上给一个 ⭐️ <b>Star</b>！这是对我们最大的鼓励与支持！</p>
</div>
