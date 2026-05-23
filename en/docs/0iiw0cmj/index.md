---
url: /en/docs/0iiw0cmj/index.md
---
# Connection String Builder (ConnectionStringBuilder)

## Overview

`ConnectionStringBuilder` is a utility class provided by the UniCon Core layer. It offers a strongly-typed, fluent (chainable) interface for dynamically assembling connection strings for various industrial protocols (S7, Modbus, MQTT, OPC UA, PubSub), completely eliminating syntax errors caused by manual string concatenation and greatly improving development efficiency.

The framework also provides quick connection extension methods for `IUniconDriver` (e.g., `ConnectS7Async`) that can be called directly on a driver instance without manually building a connection string.

## Usage

Create a protocol-specific Builder instance via the static entry of `ConnectionStringBuilder`, set properties in a chain, and call `Build()` to generate the connection string. Alternatively, use the quick extension methods directly on driver instances.

## Parameters

### S7Builder Parameters

| Parameter | Type | Description | Required | Default |
|--------|------|------|----------|--------|
| cpuType | `S7CpuType` | PLC CPU type enum (`S7200`, `S7300`, `S7400`, `S71200`, `S71500`) | Yes | `S7CpuType.S71200` |
| ip | `string` | PLC IP address | Yes | `127.0.0.1` |
| rack | `short` | Rack number | No | `0` |
| slot | `short` | Slot number | No | `1` |

### ModbusBuilder Parameters

| Parameter | Type | Description | Required | Default |
|--------|------|------|----------|--------|
| ip | `string` | Modbus slave or gateway IP address | Yes | `127.0.0.1` |
| port | `int` | Modbus service port | No | `502` |

### OpcUaPubSubBuilder Parameters

| Parameter | Type | Description | Required | Default |
|--------|------|------|----------|--------|
| scheme | `PubSubScheme` | Transport scheme (`Udp`, `Mqtt`) | Yes | `PubSubScheme.Udp` |
| host | `string` | Multicast IP or Broker IP | Yes | `224.0.2.14` |
| port | `int` | Port number | Yes | `4840` |
| topic | `string` | MQTT subscription topic path (valid only in MQTT mode) | No | `""` |

## Returns

### Build() Return Value

| Type | Description |
|------|------|
| `string` | A fully assembled, correctly formatted physical connection string |

### Quick Connection Extension Methods (e.g., ConnectS7Async)

| Type | Description |
|------|------|
| `Task<bool>` | Async boolean indicating whether the initial connection succeeded |

## Examples

**Example 1: Build an S7 connection string using the strongly-typed fluent API**

```csharp
using UniCon.Core.Helpers;

string connStr = ConnectionStringBuilder.S7()
    .WithCpuType(S7CpuType.S71500)
    .WithIp("192.168.1.10")
    .WithRack(0)
    .WithSlot(1)
    .Build();
// Output: CpuType=S71500;Ip=192.168.1.10;Rack=0;Slot=1
```

**Example 2: Use the quick connection extension method directly (Recommended)**

In projects using `IDriverRegistry`, drivers are created via factory (DI auto-assembles dependencies):

```csharp
using UniCon.Core;
using UniCon.Core.Helpers;

// Create driver via DI factory (automatically assembles all dependencies)
var driver = _driverRegistry.CreateDriver("S7", "Line1_PLC");
_driverRegistry.Register(driver);

// Strongly-typed, self-documenting quick connection extension method
bool isConnected = await driver.ConnectS7Async("192.168.1.10", S7CpuType.S71500);
```

**Example 3: Build a Modbus connection string**

```csharp
string connStr = ConnectionStringBuilder.Modbus()
    .WithIp("192.168.0.50")
    .WithPort(502)
    .Build();
// Output: ip=192.168.0.50;port=502
```

**Example 4: Build an OPC UA PubSub UDP multicast connection string**

```csharp
string connStr = ConnectionStringBuilder.OpcUaPubSub()
    .WithScheme(PubSubScheme.Udp)
    .WithHost("224.0.2.14")
    .WithPort(4840)
    .Build();
// Output: opc.udp://224.0.2.14:4840
```
