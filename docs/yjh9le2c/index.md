---
url: /docs/yjh9le2c/index.md
---
# 快速入门与集成指南

本指南介绍如何在第三方项目中引用 UniCon 框架并快速实现工业设备通讯与采集调度。

## 1. 安装与引用

UniCon 采用高度解耦的**插件化与模块化**架构设计。核心框架与各个具体物理通信驱动均已在 NuGet 仓库中作为独立的包发布。

::: tip 💡 我需要依次安装所有的 NuGet 包吗？
**不需要。**
您只需且必须安装核心依赖包 **`UniCon.Core`**，然后根据您实际的工业场景，**仅选择性地安装需要的特定协议驱动包**即可。例如，若您的项目仅需要采集 S7 PLC 数据和 Modbus 寄存器，则只需安装 `UniCon.Core`、`UniCon.Drivers.S7` 和 `UniCon.Drivers.Modbus`。这样可以避免引入多余的外部三方依赖（如 OPC UA 或 MQTTnet 库），保持您的业务项目极简、纯净与高效。
:::

### 📦 核心调度引擎 (必选)

* **.NET CLI**:
  ```bash
  dotnet add package UniCon.Core
  ```
* **Package Manager 控制台**:
  ```powershell
  Install-Package UniCon.Core
  ```

### 🔌 物理通讯驱动包 (按需选择)

| 驱动名 | 说明 | .NET CLI 安装命令 |
| :--- | :--- | :--- |
| **`UniCon.Drivers.S7`** | 西门子全系列 PLC (S7-1200/1500/300) | `dotnet add package UniCon.Drivers.S7` |
| **`UniCon.Drivers.OpcUa`** | 高性能 OPC UA 客户端通讯 | `dotnet add package UniCon.Drivers.OpcUa` |
| **`UniCon.Drivers.Modbus`** | 标准 Modbus TCP 物理设备 | `dotnet add package UniCon.Drivers.Modbus` |
| **`UniCon.Drivers.Mqtt`** | 轻量级 MQTT 消息队列发布与订阅 | `dotnet add package UniCon.Drivers.Mqtt` |
| **`UniCon.Drivers.OpcUaPubSub`** | OPC UA PubSub UDP/MQTT 驱动 | `dotnet add package UniCon.Drivers.OpcUaPubSub` |

***

## 2. 集成到 ASP.NET Core（推荐方式）

UniCon 提供开箱即用的扩展方法，一键注入注册中心、连接管理器、缓存与任务调度器。

### Program.cs 注册示例

```csharp
using UniCon.Core;
using UniCon.Core.Jobs;

var builder = WebApplication.CreateBuilder(args);

// 一键注入 UniCon 核心服务
// 包含：IDriverRegistry, IConnectionManager, INetworkMonitor,
//       OdmEngine, IUniconCacheProvider(MemoryCacheProvider)
builder.Services.AddUniCon();

// 一键注入任务调度系统（含 Quartz.NET 与内置 Job 自动扫描）
builder.Services.AddUniConJobs();

var app = builder.Build();

// 自动发现并注册所有带 [UniconDriver] 特性的驱动程序集
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
driverRegistry.DiscoverAndRegisterDrivers();

// 启动任务调度器
var jobScheduler = app.Services.GetRequiredService<JobScheduler>();
await jobScheduler.StartAsync();

app.Run();
```

***

## 3. 基础通讯示例（通过 DI 工厂创建驱动）

在 ASP.NET Core 项目中，推荐使用 `IDriverRegistry` 创建并管理驱动。驱动的所有依赖（`ILogger`、`IUniconCacheProvider`、`INetworkMonitor`）均由 DI 容器自动装配。

```csharp
using UniCon.Core;
using UniCon.Core.Models;

public class PlcService
{
    private readonly IDriverRegistry _driverRegistry;
    private readonly IConnectionManager _connectionManager;

    public PlcService(IDriverRegistry driverRegistry, IConnectionManager connectionManager)
    {
        _driverRegistry = driverRegistry;
        _connectionManager = connectionManager;
    }

    public async Task InitializeAsync()
    {
        // 通过别名创建 S7 驱动（DI 自动装配所有依赖）
        var driver = _driverRegistry.CreateDriver("S7", "PLC_Line1");
        _driverRegistry.Register(driver);

        // 启动连接（初始失败后 Watchdog 自动指数退避重连）
        await _connectionManager.RegisterDriverAsync(
            driver,
            "CpuType=S71200;Ip=192.168.0.10;Rack=0;Slot=1"
        );

        // 单点读取
        var request = new UniconRequest { Address = "DB1.DBD0" };
        var response = await driver.ReadAsync<float>(request);
        if (response.Success)
        {
            Console.WriteLine($"温度值: {response.Data?.Value} °C");
            Console.WriteLine($"数据质量: {response.Data?.Quality}");
            Console.WriteLine($"PLC时间戳: {response.Data?.SourceTimestamp}");
        }
    }
}
```

***

## 4. 主动采集订阅引擎

对于 S7、Modbus 等不具备主动推送能力的协议，UniCon v2 提供完整的主动轮询订阅引擎。

```csharp
// 结构化订阅（推荐，含死区过滤与元数据）
var subId = await driver.SubscribeAsync(new UniconSubscription
{
    Address    = "DB1.DBD0",
    ScanRateMs = 500,                         // 每 500ms 轮询一次
    ScanMode   = UniconScanMode.ExceptionBased, // 仅值或质量变化时回调
    Metadata   = new TagMetadata
    {
        Name     = "反应釜温度",
        Unit     = "℃",
        Deadband = 0.5   // 变化 < 0.5℃ 不触发通知
    },
    Callback = async dataValue =>
    {
        Console.WriteLine($"[{dataValue.ServerTimestamp:HH:mm:ss.fff}] 温度: {dataValue.Value} ℃");
        await Task.CompletedTask;
    }
});

// 按 ID 精准取消订阅
await driver.UnsubscribeByIdAsync(subId);
```

***

## 5. 批量操作

### 批量读取（S7 自动合并连续点位，最大 19 个/次）

```csharp
var requests = new List<UniconRequest>
{
    new() { Address = "DB1.DBD0" },
    new() { Address = "DB1.DBD4" },
    new() { Address = "DB1.DBX8.0" }
};

var results = await driver.ReadBatchAsync(requests);
foreach (var res in results)
{
    if (res.Success)
        Console.WriteLine($"值: {res.Data?.Value}, 质量: {res.Data?.Quality}");
}
```

### 获取扫描统计

```csharp
var stats = driver.GetStatistics(500, UniconScanMode.ExceptionBased);
if (stats != null)
{
    Console.WriteLine($"扫描次数: {stats.ScanCount}");
    Console.WriteLine($"通知次数: {stats.NotifyCount}");
    Console.WriteLine($"平均读取耗时: {stats.AverageReadDurationMs:F1}ms");
    Console.WriteLine($"错误率: {stats.ErrorRate:P2}");
}
```

***

## 6. 常见协议连接字符串参考

| 协议 | 连接字符串示例 |
| :--- | :--- |
| **S7** | `CpuType=S71200;Ip=192.168.0.10;Rack=0;Slot=1` |
| **S7（含超时）** | `CpuType=S71500;Ip=192.168.0.10;Rack=0;Slot=1;ReadTimeout=3000;WriteTimeout=3000` |
| **Modbus** | `ip=192.168.0.50;port=502;unitid=1;timeout=2000` |
| **OPC UA（匿名）** | `opc.tcp://192.168.0.100:4840` |
| **OPC UA（账户密码）** | `EndpointURL=opc.tcp://192.168.0.100:4840;Username=admin;Password=secret` |
| **MQTT** | `server=broker.hivemq.com;port=1883;clientid=UniCon_01` |
| **MQTT（TLS）** | `server=broker.example.com;port=8883;username=user;password=pwd;usetls=true` |
| **OPC UA PubSub（UDP）** | `opc.udp://224.0.2.14:4840` |
| **OPC UA PubSub（MQTT）** | `mqtt://192.168.0.200:1883/opcua/pubsub` |

***

## 7. 驱动状态监听

```csharp
driver.StateChanged += (_, e) =>
{
    Console.WriteLine($"[{e.DriverId}] 状态变更: {e.OldState} → {e.NewState}");
};
```

***

## 8. 使用快捷连接扩展方法

`ConnectionStringBuilder` 提供强类型流式 API 避免手动拼接字符串：

```csharp
using UniCon.Core.Helpers;

// 方式一：流式构建连接串
string connStr = ConnectionStringBuilder.S7()
    .WithCpuType(S7CpuType.S71500)
    .WithIp("192.168.1.10")
    .WithRack(0)
    .WithSlot(1)
    .Build();
// 输出: CpuType=S71500;Ip=192.168.1.10;Rack=0;Slot=1

// 方式二：直接使用快捷扩展方法（推荐）
var driver = _driverRegistry.CreateDriver("S7", "PLC_01");
bool isConnected = await driver.ConnectS7Async("192.168.1.10", S7CpuType.S71500);
```
