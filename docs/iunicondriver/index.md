---
url: /docs/iunicondriver/index.md
---
# IUniconDriver

## 概述 (Overview)

`IUniconDriver` 是 UniCon 框架中所有南向驱动实现必须遵循的统一接口。它定义了驱动的生命周期、基本读写、批量操作、订阅机制以及状态变更事件，确保业务层、作业系统以及 ODM 引擎能够以统一方式调用各种协议驱动（如 S7、Modbus、OPC UA 等）。

## 使用方法 (Usage)

在自定义驱动实现时继承 `DriverBase`（已实现 `IUniconDriver` 中的大多数方法），仅需重写抽象方法。构造函数必须接收并透传 `INetworkMonitor`（v2.2 起强制要求）：

```csharp
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Network;
using Microsoft.Extensions.Logging;

[UniconDriver("MyPlc")]
public class MyPlcDriver : DriverBase
{
    // 必须接收并透传全部 4 个依赖
    public MyPlcDriver(string driverId, ILogger logger,
        IUniconCacheProvider cacheProvider, INetworkMonitor networkMonitor)
        : base(driverId, logger, cacheProvider, networkMonitor) { }

    protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
    {
        // 建立实际物理连接，返回 true 表示成功
        return true;
    }

    protected override Task OnDisconnectAsync(CancellationToken ct)
    {
        // 关闭物理连接
        return Task.CompletedTask;
    }

    protected override async Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
    {
        // 实现单点读取逻辑
        throw new NotImplementedException();
    }

    protected override async Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
    {
        // 实现单点写入逻辑
        throw new NotImplementedException();
    }
}
```

## 参数说明 (Parameters)

| 方法 | 参数名 | 类型 | 说明 | 是否必填 | 默认值 |
|------|--------|------|------|----------|--------|
| `ConnectAsync` | `connectionString` | `string` | 驱动连接字符串 | ✅ | — |
|  | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `DisconnectAsync` | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `PingAsync` | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `ReadAsync<T>` | `request` | `UniconRequest` | 读取请求（地址、元数据） | ✅ | — |
|  | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `WriteAsync<T>` | `request` | `UniconRequest` | 写入请求 | ✅ | — |
|  | `value` | `T` | 写入的值 | ✅ | — |
|  | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `ReadBatchAsync` | `requests` | `IEnumerable<UniconRequest>` | 批量读取请求集合 | ✅ | — |
|  | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `WriteBatchAsync` | `writes` | `IEnumerable<(UniconRequest Request, object Value)>` | 批量写入请求集合 | ✅ | — |
|  | `ct` | `CancellationToken` | 取消令牌 | 否 | `default` |
| `SubscribeAsync(address, callback)` | `address` | `string` | 订阅的设备地址 | ✅ | — |
|  | `callback` | `Func<DataValue<object>, Task>` | 异步回调 | ✅ | — |
| `SubscribeAsync(UniconSubscription)` | `subscription` | `UniconSubscription` | 结构化订阅（含元数据、死区、扫描配置） | ✅ | — |
| `SubscribeBatchAsync` | `subscriptions` | `IEnumerable<UniconSubscription>` | 批量结构化订阅 | ✅ | — |
| `UnsubscribeAsync` | `address` | `string` | 按地址取消所有订阅 | ✅ | — |
| `UnsubscribeByIdAsync` | `subscriptionId` | `string` | 按订阅 ID 取消单个订阅 | ✅ | — |
| `UnsubscribeBatchAsync` | `addresses` | `IEnumerable<string>` | 批量按地址取消订阅 | ✅ | — |
| `UnsubscribeBatchByIdAsync` | `subscriptionIds` | `IEnumerable<string>` | 批量按 ID 取消订阅 | ✅ | — |
| `GetSubscriptions` | — | — | 返回当前已注册的所有订阅信息 | — | — |
| `GetStatistics` | `scanRateMs` | `int` | 扫描周期（毫秒） | ✅ | — |
|  | `scanMode` | `UniconScanMode` | 扫描模式（ExceptionBased / Polled） | ✅ | — |

## 驱动属性 (Properties)

| 属性名 | 类型 | 说明 |
|--------|------|------|
| `DriverId` | `string` | 驱动唯一标识 |
| `State` | `DriverState` | 当前状态：`Disconnected` / `Connecting` / `Connected` / `Faulted` |
| `IsConnected` | `bool` | 是否处于 `Connected` 状态 |
| `StateChanged` | `event` | 状态变更事件（`DriverStateChangedEventArgs`） |

## 返回值 (Returns)

| 方法 | 类型 | 说明 |
|------|------|------|
| `ConnectAsync` | `Task<bool>` | 连接成功返回 `true`，失败抛异常或返回 `false` |
| `DisconnectAsync` | `Task` | 完成断开操作 |
| `PingAsync` | `Task<bool>` | 连通性检测，`true` 表示存活 |
| `ReadAsync<T>` | `Task<UniconResponse<T>>` | 读取结果，`response.Success` 为 true 时 `response.Data` 携带值与质量戳 |
| `WriteAsync<T>` | `Task<UniconResponse<bool>>` | 写入成功返回 `true` |
| `ReadBatchAsync` | `Task<IEnumerable<UniconResponse<object>>>` | 每个请求的读取结果集合 |
| `WriteBatchAsync` | `Task<IEnumerable<UniconResponse<bool>>>` | 每个写入请求的执行结果 |
| `SubscribeAsync` / `SubscribeBatchAsync` | `Task<string>` | 成功注册返回唯一订阅 ID |
| `Unsubscribe*` 系列 | `Task` | 完成取消操作 |
| `GetSubscriptions` | `IEnumerable<UniconSubscription>` | 当前活跃订阅列表 |
| `GetStatistics` | `ScanStatistics?` | 返回指定扫描组的统计信息（次数、错误率等） |

## 使用示例 (Examples)

### 示例 1：业务服务中读取单个 Tag 值

```csharp
public class PlcService
{
    private readonly IDriverRegistry _driverRegistry;

    public PlcService(IDriverRegistry driverRegistry)
    {
        _driverRegistry = driverRegistry;
    }

    public async Task<float?> ReadTemperatureAsync(string driverId, string address)
    {
        var driver = _driverRegistry.Get(driverId)
            ?? throw new InvalidOperationException($"Driver {driverId} not found");

        var response = await driver.ReadAsync<float>(new UniconRequest { Address = address });

        if (response.Success && response.Data != null)
        {
            Console.WriteLine($"质量: {response.Data.Quality}, 时间: {response.Data.ServerTimestamp}");
            return response.Data.Value;
        }

        return null;
    }
}
```

### 示例 2：结构化订阅并处理回调

```csharp
var driver = _driverRegistry.Get("PLC_01")
    ?? throw new InvalidOperationException("Driver not found");

string subId = await driver.SubscribeAsync(new UniconSubscription
{
    Address    = "DB1.DBD0",
    ScanRateMs = 500,
    ScanMode   = UniconScanMode.ExceptionBased,
    Metadata   = new TagMetadata { Deadband = 0.5, Unit = "℃" },
    Callback   = async value =>
    {
        Console.WriteLine($"温度: {value.Value} ℃ [{value.Quality}]");
        await Task.CompletedTask;
    }
});

// 按 ID 精准取消
await driver.UnsubscribeByIdAsync(subId);
```

***

> **更新记录**：文档已同步至 `src/UniCon.Core/IUniconDriver.cs`（v2.3），修正构造函数签名（新增 `INetworkMonitor`），修正 `response.Data?.Value` 访问方式，补充属性说明表。
