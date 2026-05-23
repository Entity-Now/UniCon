---
url: /en/docs/driver-registry/index.md
---
# Driver Registry & Factory

## Overview

`IDriverRegistry` is a core infrastructure component of the UniCon framework, responsible for two major duties:

1. **Instance Lifecycle Management**: Maintains active driver instances at runtime (e.g., S7, Modbus), providing Register, Unregister, and Get APIs for cross-layer sharing.
2. **Dynamic Factory & Hot-Plugging**: Automatically scans driver assemblies via the `[UniconDriver]` attribute, supporting alias registration and alias-based or strongly-typed driver instance creation without manual configuration.

This registry combines the **Registry API** and **Factory API** to achieve unified driver management and zero-configuration assembly.

## Usage

### 1. Register Core Services

Add UniCon core services to the dependency injection container in `Program.cs` (or `Startup.cs`):

```csharp
var builder = WebApplication.CreateBuilder(args);
// Automatically injects IDriverRegistry and the DriverRegistry implementation
builder.Services.AddUniCon(); // Includes services.AddSingleton<IDriverRegistry, DriverRegistry>();
```

### 2. Auto-Discover Drivers on Startup

After the application is built, call the auto-discovery method to complete plugin-style driver loading:

```csharp
var app = builder.Build();
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
driverRegistry.DiscoverAndRegisterDrivers(); // Automatically scans and registers all drivers marked with [UniconDriver]
```

### 3. Business Layer Usage

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

    // See examples below
}
```

## Parameters

| Method | Parameter | Type | Description | Required | Default |
|------|--------|------|------|----------|--------|
| `Register` | `driver` | `IUniconDriver` | The driver instance to register | ✅ | — |
| `Unregister` | `driverId` | `string` | Unique ID of the driver to unregister | ✅ | — |
| `Get` | `driverId` | `string` | Query a registered driver instance | ✅ | — |
| `RegisterDriverType` | `driverType` | `string` | Driver alias (e.g., `"S7"`) | ✅ | — |
|  | `implementationType` | `Type` | The driver implementation class | ✅ | — |
| `CreateDriver` (alias) | `driverType` | `string` | Driver type corresponding to the alias | ✅ | — |
|  | `driverId` | `string` | Unique ID for the new instance | ✅ | — |
| `CreateDriver<T>` | `driverId` | `string` | Unique ID for the strongly-typed driver instance | ✅ | — |
| `DiscoverAndRegisterDrivers` | — | — | Automatically scans and registers all marked drivers | — | — |

## Returns

| Method | Type | Description |
|------|------|------|
| `Register` | `void` | No return; simply adds the instance to the registry |
| `Unregister` | `bool` | Returns `true` on success, `false` if not found |
| `Get` | `IUniconDriver?` | Returns the instance if found, otherwise `null` |
| `GetAll` | `IEnumerable<IUniconDriver>` | A read-only collection of all active drivers |
| `CreateDriver` | `IUniconDriver` | Creates and returns a driver instance by alias |
| `CreateDriver<T>` | `T` where `T : IUniconDriver` | Creates and returns a strongly-typed driver instance |
| `DiscoverAndRegisterDrivers` | `void` | No direct return value after auto-discovery |

## Examples

### Example 1: Zero-Friction Driver Creation & Registration using Generics

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
        // Create an S7 driver via generics; the framework automatically injects logger, cache, and other dependencies
        var plcDriver = _driverRegistry.CreateDriver<S7Driver>("PLC_01");
        // Register with the central registry
        _driverRegistry.Register(plcDriver);
        // Activate connection (example)
        await _connectionManager.RegisterDriverAsync(plcDriver, "ip=192.168.1.100;cputype=S71200");
    }
}
```

### Example 2: Plug-and-Play Creation Based on Configuration Alias

```csharp
// Assume alias and ID are read from a database or configuration file
string driverAlias = "S7"; // Driver type from business configuration
string driverId = "PLC_Line2";

// Assumes DiscoverAndRegisterDrivers() was called at application startup
IUniconDriver driver = _driverRegistry.CreateDriver(driverAlias, driverId);
_driverRegistry.Register(driver);
```

***

> **Update Record**: This documentation is synchronized with `src/UniCon.Core/IDriverRegistry.cs` (v2.3), keeping interface signatures and example code consistent.
