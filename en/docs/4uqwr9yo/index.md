---
url: /en/docs/4uqwr9yo/index.md
---
# Connection Self-Healing Manager (IConnectionManager)

## Overview

The Connection Self-Healing Manager `IConnectionManager` is a unified central component in the UniCon core framework responsible for loading all southbound physical driver lifecycles, initiating connection self-healing, querying online status, and gracefully unregistering drivers. It completely decouples specific drivers from the northbound business layer, and provides comprehensive `Task`-based async lifecycle management.

Since all driver instances inheriting from `DriverBase` have a built-in high-concurrency Watchdog thread, once `IConnectionManager` starts the initial connection, health monitoring and exponential backoff reconnection are delegated to the driver's internal mechanism. `IConnectionManager` itself acts as a global physical handle registry for running instances.

**Important**: In projects where `AddUniCon()` has already been registered, it is recommended to create drivers via `IDriverRegistry.CreateDriver()` factory method (all dependencies are auto-assembled by DI), rather than manually calling `new` on a driver instance.

## Usage

In external projects, no manual assembly is needed. Register everything with a single one-click DI extension method:

```csharp
// One-click initialization of all components in Program.cs
builder.Services.AddUniCon();
```

After registration, inject via constructor in any controller, service, or background task:

```csharp
public class Worker
{
    private readonly IConnectionManager _connectionManager;

    public Worker(IConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }
}
```

## Parameters

### 1. Register and Activate Driver Async (RegisterDriverAsync)

| Parameter | Type | Description | Required | Default |
|--------|------|------|----------|--------|
| driver | `IUniconDriver` | The driver instance to register and activate | Yes | — |
| connectionString | `string` | The protocol-specific connection string | Yes | — |
| ct | `CancellationToken` | Async cancellation token | No | `default` |

### 2. Unregister and Disconnect Driver Async (UnregisterDriverAsync)

| Parameter | Type | Description | Required | Default |
|--------|------|------|----------|--------|
| driverId | `string` | Unique driver identifier | Yes | — |
| ct | `CancellationToken` | Async cancellation token | No | `default` |

***

## Returns

### 1. RegisterDriverAsync & UnregisterDriverAsync

| Type | Description |
|------|------|
| `Task` | Async task contract; can be `await`-ed to wait for connection establishment or disconnection |

### 2. GetDriver

| Type | Description |
|------|------|
| `IUniconDriver?` | Returns the physical reference of the managed driver instance; `null` if the specified ID does not exist or has been destroyed |

***

## Examples

**Example 1: Background Worker creates and manages an S7 driver via Registry factory (Recommended)**

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniCon.Core;

public class S7ConnectorService : BackgroundService
{
    private readonly IDriverRegistry _driverRegistry;
    private readonly IConnectionManager _connectionManager;

    public S7ConnectorService(IDriverRegistry driverRegistry, IConnectionManager connectionManager)
    {
        _driverRegistry = driverRegistry;
        _connectionManager = connectionManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Create driver via factory; DI automatically injects ILogger, IUniconCacheProvider, INetworkMonitor
        var driver = _driverRegistry.CreateDriver("S7", "Line1_PLC");
        _driverRegistry.Register(driver);

        // Start connection asynchronously; Watchdog auto-heals with exponential backoff on failure
        await _connectionManager.RegisterDriverAsync(
            driver,
            "CpuType=S71500;Ip=192.168.0.10;Rack=0;Slot=1",
            stoppingToken
        );

        // Query device status
        var registeredDriver = _connectionManager.GetDriver("Line1_PLC");
        if (registeredDriver != null)
        {
            Console.WriteLine($"Driver State: {registeredDriver.State}, Connected: {registeredDriver.IsConnected}");
        }
    }
}
```

**Example 2: Dynamically unregister a driver at runtime**

```csharp
// After unregistering, the driver's Watchdog thread stops, socket connection closes, and resources are released
await _connectionManager.UnregisterDriverAsync("Line1_PLC");
```

**Example 3: Listen to driver state changes**

```csharp
var driver = _connectionManager.GetDriver("Line1_PLC");
if (driver != null)
{
    driver.StateChanged += (_, e) =>
    {
        Console.WriteLine($"[{e.DriverId}] {e.OldState} → {e.NewState}");
        // Can trigger alerts, log reporting, and other northbound linkages here
    };
}
```
