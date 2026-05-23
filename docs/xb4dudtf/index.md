---
url: /docs/xb4dudtf/index.md
---
# Web 接口与集成 (Web API & Integration)

## 概述

`UniCon.WebServer` 基于 ASP.NET Core Minimal API 提供完整的 RESTful 接口层，充当南向工业设备与北向 IT 系统之间的通用协议转换与配置桥梁。

## 整体职责

* **驱动生命周期管理**：允许在不重启服务的前提下，通过接口动态添加、查询、删除各类工业协议（S7、Modbus、OPC UA、MQTT、OPC UA PubSub）驱动实例。
* **通用数据读写**：屏蔽底层协议差异，通过统一的 HTTP 格式对任意设备点位地址进行强类型读取和写入。
* **订阅与实时缓存**：在内存中维持南向驱动的数据订阅，并在缓存中保存最新时间戳与数据质量，供北向客户端高速轮询。
* **对象-设备映射（ODM）**：通过实体类特性自动生成点位映射表，并支持批量从设备读取数据自动填充实体、反射批量写入实体属性到设备。
* **任务调度管理**：运行时动态创建、查询、更新、删除定时任务（CRUD）。

***

## 接口总览

### 1. 驱动生命周期管理 `/api/drivers`

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/drivers` | 获取所有已注册驱动的状态列表 |
| `GET` | `/api/drivers/{id}` | 查询单个驱动详情 |
| `POST` | `/api/drivers` | 动态注册并拉起新驱动（含 Watchdog 自愈） |
| `DELETE` | `/api/drivers/{id}` | 停止、注销并销毁指定驱动 |

### 2. 通用数据读写与订阅 `/api/drivers/{id}`

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/drivers/{id}/read?address=&type=` | 强类型实时读取单个点位 |
| `POST` | `/api/drivers/{id}/write` | 强类型实时写入单个点位 |
| `POST` | `/api/drivers/{id}/subscribe` | 动态注册点位订阅（缓存最新值） |
| `POST` | `/api/drivers/{id}/unsubscribe` | 取消指定点位订阅 |
| `GET` | `/api/drivers/{id}/subscriptions` | 获取该驱动所有已订阅点位的缓存数据 |
| `GET` | `/api/drivers/{id}/subscriptions/{*address}` | 获取单条订阅的实时缓存值 |

### 3. 对象-设备映射（ODM）`/api/odm`

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/odm/schema` | 反射扫描实体类，生成物理点位映射架构 |
| `GET` | `/api/odm/data` | 按需拉起驱动并批量读取实体，自动填充返回 |
| `POST` | `/api/odm/data` | 接收实体 JSON，反射批量写入物理设备 |

### 4. 任务调度管理 `/api/jobs`

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/api/jobs/executing-count` | 获取当前正在执行的任务数量 |
| `GET` | `/api/jobs` | 获取所有已调度任务列表 |
| `GET` | `/api/jobs/{id}` | 查询单个任务的详细信息 |
| `POST` | `/api/jobs` | 动态创建并安排新任务 |
| `PUT` | `/api/jobs/{id}` | 更新已安排任务的 Cron 表达式及参数 |
| `DELETE` | `/api/jobs/{id}` | 注销并彻底删除指定任务 |

***

## 初始化配置

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUniCon();       // 注入驱动注册中心、连接管理器、缓存、ODM 引擎
builder.Services.AddUniConJobs();   // 注入任务调度系统

var app = builder.Build();

// 自动发现并注册所有带 [UniconDriver] 的驱动
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
driverRegistry.DiscoverAndRegisterDrivers();

// 启动任务调度器
var jobScheduler = app.Services.GetRequiredService<JobScheduler>();
await jobScheduler.StartAsync();

app.Run();
```
