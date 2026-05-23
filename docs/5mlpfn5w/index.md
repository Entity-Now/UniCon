---
url: /docs/5mlpfn5w/index.md
---
# HTTP 任务 (HttpJob)

## 概述 (Overview)

`HttpJob` 是 UniCon 内置的 HTTP 请求定时任务，基于 `IHttpClientFactory` 实现，支持 GET、POST、PUT、DELETE 等所有标准 HTTP 方法。可配置请求头、Query 参数及请求体，适用于定时向云平台 Webhook、RESTful API 发送数据或心跳包。

任务执行结果记录在日志系统中，不抛出异常（异常时降级为日志记录），保证调度器稳定运行。

## 使用方法 (Usage)

通过 `JobScheduler.ScheduleJobAsync<HttpJob>()` 注册任务，使用 `JobDataMap` 传入参数：

```csharp
await jobScheduler.ScheduleJobAsync<HttpJob>(
    jobId: "PushDataToCloud",
    cronExpression: "0 0/5 * * * ?",
    data: new JobDataMap
    {
        [JobDataKeys.HttpUrl]    = "https://api.example.com/v1/data",
        [JobDataKeys.HttpMethod] = "POST",
        [JobDataKeys.HttpBody]   = "{\"deviceId\": \"PLC_01\", \"status\": \"running\"}",
        [JobDataKeys.HttpHeaders]= "{\"Authorization\": \"Bearer token123\", \"Content-Type\": \"application/json\"}"
    }
);
```

## 参数说明 (Parameters)

通过 `JobDataMap` 传递以下键值（使用 `JobDataKeys` 常量类访问，避免拼写错误）：

| JobDataKeys 常量 | 实际键名 | 类型 | 说明 | 是否必填 | 默认值 |
|-----------------|---------|------|------|----------|--------|
| `JobDataKeys.HttpUrl` | `Job_Http_Url` | `string` | 目标 URL | 是 | — |
| `JobDataKeys.HttpMethod` | `Job_Http_Method` | `string` | 请求方法（`GET`、`POST`、`PUT`、`DELETE` 等） | 否 | `"GET"` |
| `JobDataKeys.HttpHeaders` | `Job_Http_Headers` | `string` | JSON 序列化的请求头字典 `Dictionary<string, string>` | 否 | 无 |
| `JobDataKeys.HttpQueryParams` | `Job_Http_QueryParams` | `string` | JSON 序列化的 Query 参数字典 `Dictionary<string, string>` | 否 | 无 |
| `JobDataKeys.HttpBody` | `Job_Http_Body` | `string` | 请求体字符串（固定以 `application/json` 编码发送） | 否 | 无 |

## 返回值 (Returns)

`HttpJob` 为异步执行任务，无直接返回值。执行结果通过日志系统记录：

| 情况 | 日志级别 | 内容 |
|------|---------|------|
| 执行开始 | `Information` | `Executing HttpJob: {Method} {Url}` |
| 执行成功 | `Information` | `HttpJob response: {StatusCode}` |
| URL 为空 | `Warning` | `HttpJob skipped: URL is null or empty.` |
| 执行失败 | `Error` | `HttpJob request failed. {Exception}` |
| Query 参数解析失败 | `Error` | `Failed to parse HttpJob query parameters. {Exception}` |
| 请求头解析失败 | `Error` | `Failed to apply HttpJob headers. {Exception}` |

## 使用示例 (Examples)

**示例 1：每 5 分钟 POST 数据到云端 Webhook**

```csharp
await jobScheduler.ScheduleJobAsync<HttpJob>(
    jobId: "PushDataToCloud",
    cronExpression: "0 0/5 * * * ?",  // 每 5 分钟
    data: new JobDataMap
    {
        [JobDataKeys.HttpUrl]     = "https://api.example.com/v1/data",
        [JobDataKeys.HttpMethod]  = "POST",
        [JobDataKeys.HttpBody]    = "{\"deviceId\": \"PLC_01\", \"status\": \"running\"}",
        [JobDataKeys.HttpHeaders] = "{\"Authorization\": \"Bearer token123\"}"
    }
);
```

**示例 2：带 Query 参数的定时 GET 请求**

```csharp
await jobScheduler.ScheduleJobAsync<HttpJob>(
    jobId: "QueryCloudConfig",
    cronExpression: "0 0 * * * ?",  // 每小时
    data: new JobDataMap
    {
        [JobDataKeys.HttpUrl]         = "https://config.example.com/api/settings",
        [JobDataKeys.HttpMethod]      = "GET",
        [JobDataKeys.HttpQueryParams] = "{\"gatewayId\": \"GW_01\", \"version\": \"2\"}"
    }
);
```

**示例 3：每 5 分钟 GET 心跳检测**

```csharp
await jobScheduler.ScheduleJobAsync<HttpJob>(
    jobId: "Heartbeat",
    cronExpression: "0 0/5 * * * ?",
    data: new JobDataMap
    {
        [JobDataKeys.HttpUrl]    = "https://api.example.com/heartbeat",
        [JobDataKeys.HttpMethod] = "GET"
    }
);
```
