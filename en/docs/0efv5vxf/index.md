---
url: /en/docs/0efv5vxf/index.md
---
# Core Architecture

## Overview

The Core module is the heart of UniCon. It defines all system contracts, model structures, base self-healing services, unified caching, and the IoC registration entry point.

## Key Features

* **Interface Definition (`IUniconDriver`)**: The unified southbound driver contract, covering instant read/write, batch read/write, keep-alive probing, async subscriptions, state change events, and runtime statistics queries.
* **Message Models**: `UniconRequest` / `UniconResponse<T>` for structured interaction; `DataValue<T>` carries `SourceTimestamp` (PLC time), `ServerTimestamp` (gateway arrival time), `DataStatus`, and `QualityCode` (OPC UA-compatible quality code).
* **Driver Registry (`IDriverRegistry`)**: Unified sharing, dynamic querying, and status polling for all driver instances across the system.
* **Connection Self-Healing Manager (`IConnectionManager`)**: Manages the physical connections of all drivers throughout their entire lifecycle, working in conjunction with the driver's built-in Watchdog exponential backoff reconnection mechanism.
* **Object-Device Mapping Engine (`OdmEngine`)**: An ORM-like design that automatically maps business entity properties to device register addresses via strongly-typed attributes, enabling dynamic read/write without hardcoding.
* **Scan Scheduling Engine (v2)**:
  * `ScanGroupRegistry`: Pre-groups subscriptions by `(ScanRate, ScanMode)` on registration, deduplicating addresses.
  * `ScanScheduler`: Dynamically schedules expired `ScanGroup`s using a minimum-wait-time algorithm, replacing the `while(true)+Delay(50)` full-scan approach.
  * `NotificationDispatcher`: `Channel<T>` async dispatch with independent consumer threads; callback exceptions are isolated and do not affect the scan thread.
  * `IScanStrategy`: Strategy pattern replacing `if(ScanMode==...)` branches, with built-in `ExceptionBased` (deadband support) and `Polled` implementations.
* **Tag Metadata (`TagMetadata`)**: Describes a tag's data type, read/write permissions, engineering units, linear scaling, and deadband threshold.
* **Runtime Statistics (`ScanStatistics`)**: Collects each `ScanGroup`'s scan count, notification count, average read duration, and error rate in a lock-free manner using `Interlocked`.
* **Transport Layer Abstraction (`ITransport`)**: Separates byte transmission from protocol parsing, supporting concurrent reads/exclusive writes with lock granularity pushed down to the Transport layer.
* **Pluggable Cache (`IUniconCacheProvider`)**: DI-injected (non-static), with a built-in `MemoryCacheProvider` that can be replaced with a distributed implementation like Redis.
* **Out-of-the-Box Integration (`ServiceCollectionExtensions`)**: A one-click `AddUniCon()` extension that registers the driver registry, connection manager, cache, and more into the IoC container as singletons.
* **Connection Builder (`ConnectionStringBuilder`)**: A strongly-typed, fluent API for dynamically assembling connection strings for each protocol.
