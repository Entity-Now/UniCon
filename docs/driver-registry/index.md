---
url: /docs/driver-registry/index.md
---
# 驱动注册与创建中心 (Driver Registry & Factory)

## 概述 (Overview)

`IDriverRegistry` 是 UniCon 框架的核心基础设施，负责两大职责：

1. **实例生命周期管理**：维护运行时活跃的驱动实例（如 S7、Modbus），提供注册、注销、查询等 API，实现跨层共享。
2. **动态工厂与即插即用**：通过 `[UniconDriver]` 特性自动扫描驱动程序集，支持别名注册、基于别名或强类型创建驱动实例，免除手动配置。

该中心结合了 **Registry API** 与 **Factory API**，实现了驱动的统一托管与零配置装配。

## 使用方法 (Usage)

### 1. 注册核心服务

在 `Program.cs`（或 `Startup.cs`）的依赖注入阶段加入 UniCon 核心服务：

```csharp
var builder = WebApplication.CreateBuilder(args);
// 自动注入 IDriverRegistry 与 DriverRegistry 实现
builder.Services.AddUniCon(); // 包含 services.AddSingleton<IDriverRegistry, DriverRegistry>();
```

### 2. 启动时自动发现驱动

在应用构建完成后调用自发现方法，完成插件式驱动加载：

```csharp
var app = builder.Build();
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
driverRegistry.DiscoverAndRegisterDrivers(); // 自动扫描并注册所有带 [UniconDriver] 的驱动
```

### 3. 业务层使用

```csharp
public class PlcManager
{
    private readonly IDriverRegistry _driverRegistry;
    private readonly IConnectionManager _connectionManager;

    public PlcManager(IDriverRegistry driverRegistry, IConnectionManager connectionManager)
    {
        _driverRegistry = driverRegistry;
        _connectionManager = connectionManager;
    }

    // 示例见后文
}
```

## 参数说明 (Parameters)

| 方法 | 参数名 | 类型 | 说明 | 是否必填 | 默认值 |
|------|--------|------|------|----------|--------|
| `Register` | `driver` | `IUniconDriver` | 待托管的驱动实例 | ✅ | — |
| `Unregister` | `driverId` | `string` | 需要注销的驱动唯一标识 | ✅ | — |
| `Get` | `driverId` | `string` | 查询已托管的驱动实例 | ✅ | — |
| `RegisterDriverType` | `driverType` | `string` | 驱动别名（如 `"S7"`） | ✅ | — |
|  | `implementationType` | `Type` | 驱动实现类 | ✅ | — |
| `CreateDriver` (别名) | `driverType` | `string` | 别名对应的驱动类型 | ✅ | — |
|  | `driverId` | `string` | 新实例唯一标识 | ✅ | — |
| `CreateDriver<T>` | `driverId` | `string` | 强类型驱动实例唯一标识 | ✅ | — |
| `DiscoverAndRegisterDrivers` | — | — | 自动扫描并注册所有标记的驱动 | — | — |

## 返回值 (Returns)

| 方法 | 类型 | 说明 |
|------|------|------|
| `Register` | `void` | 无返回，仅将实例加入托管中心 |
| `Unregister` | `bool` | 注销成功返回 `true`，未找到返回 `false` |
| `Get` | `IUniconDriver?` | 若存在返回实例，否则 `null` |
| `GetAll` | `IEnumerable<IUniconDriver>` | 所有活跃驱动的只读集合 |
| `CreateDriver` | `IUniconDriver` | 创建并返回指定别名的驱动实例 |
| `CreateDriver<T>` | `T` where `T : IUniconDriver` | 强类型创建并返回驱动实例 |
| `DiscoverAndRegisterDrivers` | `void` | 自动发现后无直接返回值 |

## 使用示例 (Examples)

### 示例 1：泛型方式零摩擦创建并注册驱动

```csharp
public class PlcManager
{
    private readonly IDriverRegistry _driverRegistry;
    private readonly IConnectionManager _connectionManager;

    public PlcManager(IDriverRegistry driverRegistry, IConnectionManager connectionManager)
    {
        _driverRegistry = driverRegistry;
        _connectionManager = connectionManager;
    }

    public async Task InitializeLine1PlcAsync()
    {
        // 通过泛型创建 S7 驱动，框架自动装配日志、缓存等依赖
        var plcDriver = _driverRegistry.CreateDriver<S7Driver>("PLC_01");
        // 注册至中心托管
        _driverRegistry.Register(plcDriver);
        // 激活连接（示例）
        await _connectionManager.RegisterDriverAsync(plcDriver, "ip=192.168.1.100;cputype=S71200");
    }
}
```

### 示例 2：基于配置别名的即插即用创建

```csharp
// 假设从数据库或配置文件读取到别名与 ID
string driverAlias = "S7"; // 业务配置的驱动类型
string driverId = "PLC_Line2";

// 已在程序启动时调用 DiscoverAndRegisterDrivers()
IUniconDriver driver = _driverRegistry.CreateDriver(driverAlias, driverId);
_driverRegistry.Register(driver);
```

***

> **更新记录**：本次文档同步至 `src/UniCon.Core/IDriverRegistry.cs`（v2.3），保持接口签名与示例代码一致。
