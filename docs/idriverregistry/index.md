---
url: /docs/idriverregistry/index.md
---
# IDriverRegistry

## 概述 (Overview)

`IDriverRegistry` 是 UniCon 框架的 **驱动注册中心**，负责管理运行时活跃的驱动实例并提供统一的工厂创建能力。它将 **实例生命周期管理 (Registry API)** 与 **别名工厂 (Factory API)** 融合，为业务层、作业系统、ODM 引擎提供统一的驱动访问入口。

## 使用方法 (Usage)

```csharp
var builder = WebApplication.CreateBuilder(args);
// 注入核心服务（其中已包含 IDriverRegistry）
builder.Services.AddUniCon();

var app = builder.Build();
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
// 自动发现并注册所有标记的驱动
driverRegistry.DiscoverAndRegisterDrivers();
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
| `DiscoverAndRegisterDrivers` | — | — | 自动扫描所有带 `[UniconDriver]` 标记的驱动并注册 | — | — |

## 返回值 (Returns)

| 方法 | 类型 | 说明 |
|------|------|------|
| `Register` | `void` | 无返回，仅将实例加入托管中心 |
| `Unregister` | `bool` | 成功返回 `true`，若不存在返回 `false` |
| `Get` | `IUniconDriver?` | 若存在返回实例，否则 `null` |
| `GetAll` | `IEnumerable<IUniconDriver>` | 所有活跃驱动的只读集合 |
| `CreateDriver` | `IUniconDriver` | 创建并返回指定别名的驱动实例 |
| `CreateDriver<T>` | `T` where `T : IUniconDriver` | 强类型创建并返回驱动实例 |
| `DiscoverAndRegisterDrivers` | `void` | 自动发现后无直接返回值 |

## 使用示例 (Examples)

### 示例 1：在业务服务中获取并使用驱动

```csharp
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
        // 通过别名创建驱动并注册
        var driver = _driverRegistry.CreateDriver("S7", "PLC_01");
        _driverRegistry.Register(driver);
        await _connectionManager.RegisterDriverAsync(driver, "ip=192.168.1.100;cputype=S71200");
    }
}
```

### 示例 2：强类型泛型创建驱动

```csharp
var driver = _driverRegistry.CreateDriver<S7Driver>("PLC_02");
_driverRegistry.Register(driver);
```

***

> **更新记录**：文档已同步至 `src/UniCon.Core/IDriverRegistry.cs`（v2.3），确保所有方法签名、返回类型与示例代码保持一致。
