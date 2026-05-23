---
url: /docs/tmpzgjr3/index.md
---
# OPC UA PubSub 驱动 (OPC UA PubSub Driver)

## 概述 (Overview)

本驱动实现了 OPC UA Part 14 规范定义的发布-订阅 (PubSub) 模型通讯。与传统的 Request/Response 模式（如 S7、Modbus）不同，PubSub 提供极低延迟的单向高速数据流传输，特别适合工厂级别的软实时与硬实时通讯。

支持双栈接入：

1. **UADP over UDP 多播**：通过 `UadpPubSubDecoder` 动态解包高频多播网络数据，自动映射质量属性与高精度源时间戳。
2. **JSON/Binary over MQTT**：支持标准自描述 JSON Payload，通过 `?encoding=binary` 参数强制切换 UADP 二进制解析。

**重要限制**：PubSub 为单向数据流，**不支持 `ReadAsync`、`WriteAsync`、`ReadBatchAsync`、`WriteBatchAsync`**，调用时抛出 `NotSupportedException`。仅通过 `SubscribeAsync` / `UnsubscribeAsync` 接收数据。

底层传输层通过 `IPubSubTransport.ConnectionLost` 事件感知连接中断，由 `DriverBase` Watchdog 自动处理重连。

## 使用方法 (Usage)

通过 `IDriverRegistry` 工厂方法创建（推荐，DI 自动装配依赖）：

```csharp
// UDP 组播
var driver = _driverRegistry.CreateDriver("OpcUaPubSub", "PubSub_UDP");
_driverRegistry.Register(driver);
await _connectionManager.RegisterDriverAsync(driver, "opc.udp://224.0.2.14:4840");
```

驱动根据连接字符串的协议头自动选择传输组件与解码器：

* `opc.udp://` → `UdpPubSubTransport` + `UadpPubSubDecoder`（二进制）
* `mqtt://` + 无 `encoding=binary` → `MqttPubSubTransport` + `JsonPubSubDecoder`（JSON）
* `mqtt://` + `?encoding=binary` → `MqttPubSubTransport` + `UadpPubSubDecoder`（二进制）

## 参数说明 (Parameters)

### 连接字符串 (ConnectionString / URI)

| 格式 | 示例 | 说明 |
|------|------|------|
| UDP 组播 | `opc.udp://224.0.2.14:4840` | 加入 UDP 多播组 |
| MQTT JSON | `mqtt://192.168.1.100:1883/factory/line1` | 订阅 MQTT Topic，JSON 解码 |
| MQTT 二进制 | `mqtt://192.168.1.100:1883/factory/line1?encoding=binary` | 订阅 MQTT Topic，UADP 二进制解码 |

### 订阅地址 (SubscribeAsync address)

地址对应 PubSub 解码后的**数据集字段名称**（Dataset Field Name），由 Publisher 方在 UADP/JSON 消息中定义，如 `"Temperature"`、`"MotorSpeed"`。

## 返回值 (Returns)

| 接口方法 | 返回值 | 说明 |
|----------|--------|------|
| `SubscribeAsync` | `Task<string>` | 返回 address（字段名）作为订阅 ID |
| `UnsubscribeAsync` | `Task` | 移除对该字段名的订阅 |
| `PingAsync` | `Task<bool>` | 若 Transport 非 null（已连接）返回 `true` |
| `ReadAsync` | — | **不支持**，抛出 `NotSupportedException` |
| `WriteAsync` | — | **不支持**，抛出 `NotSupportedException` |

订阅回调接收到的数据：

| 字段 | 类型 | 说明 |
|------|------|------|
| `Value` | `object?` | 解码后的实际数值 |
| `Status` | `DataStatus` | `Good` / `Bad` / `Uncertain` |
| `SourceTimestamp` | `DateTime` | 数据在 Publisher 端产生的时间戳 |
| `ServerTimestamp` | `DateTime` | 网关接收到数据的时间戳 |

## 使用示例 (Examples)

**示例 1：UDP UADP 多播订阅**

```csharp
var driver = _driverRegistry.CreateDriver("OpcUaPubSub", "PubSub_UDP");
_driverRegistry.Register(driver);
await _connectionManager.RegisterDriverAsync(driver, "opc.udp://224.0.2.14:4840");

// address 为 Publisher 发布的数据集字段名
await driver.SubscribeAsync("Temperature", async data =>
{
    Console.WriteLine($"[UDP UADP] 温度: {data.Value}, 质量: {data.Status}, 源时间: {data.SourceTimestamp}");
    await Task.CompletedTask;
});
```

**示例 2：MQTT UADP 二进制订阅（强制二进制解码）**

```csharp
var driver = _driverRegistry.CreateDriver("OpcUaPubSub", "PubSub_MQTT_Binary");
_driverRegistry.Register(driver);

// 附加 ?encoding=binary 强制使用 UadpPubSubDecoder
await _connectionManager.RegisterDriverAsync(
    driver,
    "mqtt://192.168.1.100:1883/factory/line1/uadp?encoding=binary"
);

await driver.SubscribeAsync("MotorSpeed", async data =>
{
    Console.WriteLine($"[MQTT UADP] 转速: {data.Value} RPM, 质量: {data.Status}");
    await Task.CompletedTask;
});
```

**示例 3：MQTT JSON 格式订阅（默认 JSON 解码）**

```csharp
var driver = _driverRegistry.CreateDriver("OpcUaPubSub", "PubSub_MQTT_JSON");
_driverRegistry.Register(driver);
await _connectionManager.RegisterDriverAsync(driver, "mqtt://192.168.0.100:1883/factory/line1/json");

await driver.SubscribeAsync("Current", async data =>
{
    Console.WriteLine($"[MQTT JSON] 电流: {data.Value} A, 接收质量: {data.Status}");
    await Task.CompletedTask;
});
```

**示例 4：使用 ConnectionStringBuilder 生成连接串**

```csharp
using UniCon.Core.Helpers;

string connStr = ConnectionStringBuilder.OpcUaPubSub()
    .WithScheme(PubSubScheme.Udp)
    .WithHost("224.0.2.14")
    .WithPort(4840)
    .Build();
// 输出: opc.udp://224.0.2.14:4840
```
