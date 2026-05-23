---
url: /docs/p5uyhklb/index.md
---
# Driver Architecture v2 — 扫描调度引擎

## 概述 (Overview)

v2.3 对 `DriverBase` 实现了职责拆分与精简协调层，核心组件包括：

| 组件 | 职责 |
|------|------|
| `DriverBase` | 轻量协调层：连接、Watchdog、状态管理 |
| `IConnectionManager` | 连接/断开、Watchdog 拉起、物理句柄托管 |
| `INetworkMonitor` | 全局网络可达性监测（断网挂起、上线唤醒） |
| `ScanGroupRegistry` | 订阅注册时完成地址去重与分组 |
| `ScanScheduler` | 基于最小等待时间调度到期 ScanGroup |
| `NotificationDispatcher` | `Channel<T>` 异步分发，回调隔离 |
| `IUniconCacheProvider` | DI 注入缓存，非 static |
| `IScanStrategy` | 策略模式：`ExceptionBased`（死区支持）与 `Polled` |

***

## 数据流

```
Subscribe(address, callback)
    │
    ▼
ScanGroupRegistry.GetOrCreate(rate, mode)
    │  注册时完成地址去重
    ▼
TagEntry.AddSubscriber(id, callback)

─────────────────────────────────────────

ScanScheduler（独立线程）
    │  最小等待时间调度
    ├─► 找到到期 ScanGroup
    │       │
    │       ▼
    │   Driver.ReadBatchAsync(addresses)  ← 真实批量读取
    │       │
    │       ▼
    │   IScanStrategy.ShouldNotify?  ← ExceptionBased / Polled
    │       │  yes
    │       ▼
    │   TagEntry.UpdateCache(value)
    │   CacheProvider.SetAsync(fire-and-forget)
    │       │
    │       ▼
    │   NotificationDispatcher.TryEnqueue(envelope)

─────────────────────────────────────────

NotificationDispatcher（独立消费线程）
    │  Channel<NotificationEnvelope>
    ▼
await envelope.Callback(value)  ← 异常隔离，不影响扫描线程
```

***

## 使用方法 (Usage)

### 注册订阅

```csharp
// 简写（async Func callback）
var subId = await driver.SubscribeAsync("DB1.DBD0", async value =>
{
    Console.WriteLine($"New value: {value.Value} [{value.Quality}]");
    await Task.CompletedTask;
});

// 结构化订阅（含元数据与死区）
var subId = await driver.SubscribeAsync(new UniconSubscription
{
    Address    = "DB1.DBD0",
    ScanRateMs = 500,
    ScanMode   = UniconScanMode.ExceptionBased,
    Metadata   = new TagMetadata { Deadband = 0.5, Unit = "℃" },
    Callback   = async value => { /* ... */ }
});
```

### 取消订阅

```csharp
await driver.UnsubscribeByIdAsync(subId);
// 或按地址取消所有订阅
await driver.UnsubscribeAsync("DB1.DBD0");
```

### 查询统计

```csharp
var stats = driver.GetStatistics(500, UniconScanMode.ExceptionBased);
Console.WriteLine($"扫描次数: {stats?.ScanCount}, 通知次数: {stats?.NotifyCount}");
Console.WriteLine($"平均读取: {stats?.AverageReadDurationMs:F1}ms, 错误率: {stats?.ErrorRate:P1}");
```

### 监听状态变更

```csharp
driver.StateChanged += (_, e) =>
{
    Console.WriteLine($"[{e.DriverId}] {e.OldState} → {e.NewState}");
};
```

***

## 参数说明 (Parameters)

### UniconSubscription

| 参数 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `Address` | `string` | 寄存器地址，如 `"DB1.DBD0"` | 必填 |
| `ScanRateMs` | `int` | 轮询周期（毫秒） | `1000` |
| `ScanMode` | `UniconScanMode` | `ExceptionBased` / `Polled` | `ExceptionBased` |
| `Callback` | `Func<DataValue<object>, Task>` | 异步通知回调 | 必填 |
| `MaxQueueLength` | `int` | 通知队列最大长度 | `128` |
| `OverflowPolicy` | `OverflowPolicy` | `DropOldest` / `DropNewest` | `DropOldest` |
| `Metadata` | `TagMetadata?` | 死区/单位/类型元数据 | `null` |

### TagMetadata

| 参数 | 类型 | 说明 |
|------|------|------|
| `Deadband` | `double` | 数值变化死区（工程单位），`ExceptionBased` 模式生效 |
| `Unit` | `string` | 工程单位，如 `"℃"` |
| `DataType` | `UniconDataType` | 期望的数据类型 |
| `ScalingFactor` | `double` | 线性缩放系数 |
| `AccessMode` | `TagAccessMode` | `ReadOnly` / `WriteOnly` / `ReadWrite` |

***

## 实现自定义 Driver (Examples)

继承 `DriverBase`，构造函数中必须传入并透传 `INetworkMonitor`（**v2.2 新增必填依赖**）：

```csharp
using UniCon.Core;
using UniCon.Core.Caching;
using UniCon.Core.Models;
using UniCon.Core.Network;
using Microsoft.Extensions.Logging;

[UniconDriver("MyPlc")]  // 注册别名，DiscoverAndRegisterDrivers() 自动发现
public class MyPlcDriver : DriverBase
{
    private MyPhysicalClient? _physicalClient;

    // 必须接收并透传所有 4 个依赖
    public MyPlcDriver(string driverId, ILogger logger,
        IUniconCacheProvider cacheProvider, INetworkMonitor networkMonitor)
        : base(driverId, logger, cacheProvider, networkMonitor) { }

    protected override async Task<bool> OnConnectAsync(string connectionString, CancellationToken ct)
    {
        // 建立物理连接
        _physicalClient = new MyPhysicalClient(connectionString);
        await _physicalClient.ConnectAsync(ct);
        return _physicalClient.IsConnected;
    }

    protected override async Task OnDisconnectAsync(CancellationToken ct)
    {
        // 关闭物理连接
        if (_physicalClient != null)
            await _physicalClient.DisconnectAsync(ct);
    }

    protected override async Task<UniconResponse<T>> InternalReadAsync<T>(UniconRequest request, CancellationToken ct)
    {
        // 单点读取（由 DriverBase 默认 ReadBatchAsync 循环调用）
        var raw = await _physicalClient!.ReadAsync(request.Address, ct);
        return UniconResponse<T>.CreateSuccess((T)Convert.ChangeType(raw, typeof(T)));
    }

    protected override async Task<UniconResponse<bool>> InternalWriteAsync<T>(UniconRequest request, T value, CancellationToken ct)
    {
        await _physicalClient!.WriteAsync(request.Address, value, ct);
        return UniconResponse<bool>.CreateSuccess(true);
    }

    /// <summary>
    /// 覆盖以提供真实批量读取（地址块合并），替代 DriverBase 的 foreach 兜底
    /// </summary>
    public override async Task<IEnumerable<UniconResponse<object>>> ReadBatchAsync(
        IEnumerable<UniconRequest> requests, CancellationToken ct = default)
    {
        // 实现地址块合并，一次 IO 读取多个连续地址
        var results = new List<UniconResponse<object>>();
        // ... 合并逻辑 ...
        return results;
    }

    public override void Dispose()
    {
        _physicalClient?.Dispose();
        base.Dispose(); // 必须调用：释放调度器、退订网络事件
    }
}
```

***

## 关键设计决策

### 为什么移除全局 `_syncLock`？

原有 `SemaphoreSlim(1,1)` 对所有 Read/Write 串行化，阻止并发读。新架构锁下沉到 `ITransport` 层，由各协议实现决定并发策略（如 S7.Net 本身已内置线程安全）。

### 为什么 `ScanMode` 改为策略模式？

`if (ScanMode == ExceptionBased)` 分支散落在扫描循环中，每添加新模式（如 Deadband、Sampling）都需修改核心调度逻辑。`IScanStrategy` 将判断逻辑封装为可替换对象，新模式无需修改 Scheduler。

### 为什么用 `Channel<T>` 而非直接 `await callback()`？

直接 `await` 回调会让用户代码阻塞扫描线程。`Channel<T>` 将采集与通知完全解耦：

* 扫描线程只做 `TryWrite`（非阻塞）
* 独立消费线程异步 `await callback()`
* 单个回调异常不影响后续通知消费

### 为什么 LastPolledTime 从 Subscription 迁移到 ScanGroup？

同一 ScanGroup 内所有 Tag 同批次读取，共享同一个调度周期。每个 Subscription 持有独立的 LastPolledTime 会导致同组 Tag 在不同时刻各自触发，产生多次不必要的 IO。

### 为什么构造函数新增 `INetworkMonitor`？

`INetworkMonitor` 提供全局网络状态（Ping/TCP）监测，当整个网络不可达时，Watchdog 自动挂起重连循环，避免无效的 CPU 消耗和 TCP 握手风暴。网络恢复后，所有挂起的 Watchdog 同时被唤醒并在毫秒级内完成驱动自愈。
