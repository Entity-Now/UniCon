using Microsoft.AspNetCore.Mvc;
using Quartz;
using UniCon.WebServer.Services;
using UniCon.Core;
using UniCon.Core.Odm;
using UniCon.Core.Jobs;
using UniCon.Core.Jobs.BuiltIn;
using UniCon.WebServer.Models;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddUniCon();
builder.Services.AddUniConJobs();
builder.Services.AddSingleton<CommunicationService>();

var app = builder.Build();

// Discover and register concrete driver types using reflection assembly scanning (v2.3)
var driverRegistry = app.Services.GetRequiredService<IDriverRegistry>();
driverRegistry.DiscoverAndRegisterDrivers();

// Initialize and start the job scheduler
var jobScheduler = app.Services.GetRequiredService<JobScheduler>();
await jobScheduler.StartAsync();

// Example: Schedule a built-in HttpJob (runs in background)
await jobScheduler.ScheduleJobAsync<HttpJob>(
    "CheckGoogle",
    "0 0/5 * * * ?", // Every 5 minutes
    new JobDataMap
    {
        [JobDataKeys.HttpUrl] = "https://www.google.com",
        [JobDataKeys.HttpMethod] = "GET"
    }
);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

#region 1. 驱动生命周期管理接口组 (Driver Lifecycle Management)

var driversGroup = app.MapGroup("/api/drivers").WithOpenApi();

// GET /api/drivers - 获取当前所有已注册运行的驱动状态列表
driversGroup.MapGet("/", (CommunicationService service) =>
{
    var list = service.GetDrivers();
    return Results.Ok(list);
})
.WithName("GetDriversList")
.WithSummary("获取所有驱动状态")
.WithDescription("返回系统中当前注册的所有通讯驱动的类型、物理连接状态及连接字符串。");

// GET /api/drivers/{id} - 获取单个驱动详情
driversGroup.MapGet("/{id}", (string id, CommunicationService service) =>
{
    var driver = service.GetDriver(id);
    return driver != null ? Results.Ok(driver) : Results.NotFound(new { Message = $"Driver '{id}' not found." });
})
.WithName("GetDriverDetails")
.WithSummary("查询单个驱动状态");

// POST /api/drivers - 动态注册、实例化并拉起新驱动的自愈 Watchdog 线程
driversGroup.MapPost("/", async ([FromBody] CreateDriverRequest request, CommunicationService service) =>
{
    try
    {
        var status = await service.CreateDriverAsync(request.DriverId, request.DriverType, request.ConnectionString);
        return Results.Created($"/api/drivers/{status.DriverId}", status);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { Message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { Message = ex.Message });
    }
    catch (NotSupportedException ex)
    {
        return Results.BadRequest(new { Message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("CreateDriver")
.WithSummary("动态创建并拉起新驱动");

// DELETE /api/drivers/{id} - 动态彻底销毁断开某驱动
driversGroup.MapDelete("/{id}", async (string id, CommunicationService service) =>
{
    var success = await service.RemoveDriverAsync(id);
    return success
        ? Results.Ok(new { Message = $"Driver '{id}' has been disconnected, unregistered, and disposed." })
        : Results.NotFound(new { Message = $"Driver '{id}' was not found." });
})
.WithName("DeleteDriver")
.WithSummary("注销并关闭指定驱动");

#endregion

#region 2. 通用交互与订阅控制接口组 (Universal Driver IO & Subscriptions)

var ioGroup = app.MapGroup("/api/drivers/{id}").WithOpenApi();

// GET /api/drivers/{id}/read - 通用实时读取单个标签或点位地址的值 (带强类型映射)
ioGroup.MapGet("/read", async (string id, [FromQuery] string address, [FromQuery] string? type, CommunicationService service) =>
{
    if (string.IsNullOrWhiteSpace(address))
    {
        return Results.BadRequest(new { Message = "Query parameter 'address' is required." });
    }

    var response = await service.ReadAddressAsync(id, address, type ?? "object");
    return response.Success
        ? Results.Ok(response.Data)
        : Results.Json(response, statusCode: response.StatusCode == 404 ? 404 : 500);
})
.WithName("ReadAddress")
.WithSummary("通用实时物理点位读取");

// POST /api/drivers/{id}/write - 通用强类型写入物理点位值
ioGroup.MapPost("/write", async (string id, [FromBody] WriteAddressRequest request, CommunicationService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Address))
    {
        return Results.BadRequest(new { Message = "Request payload must contain a valid address." });
    }

    var response = await service.WriteAddressAsync(id, request.Address, request.Value, request.TypeHint);
    return response.Success
        ? Results.Ok(response)
        : Results.Json(response, statusCode: response.StatusCode == 404 ? 404 : (response.StatusCode == 400 ? 400 : 500));
})
.WithName("WriteAddress")
.WithSummary("通用实时物理点位写入");

// POST /api/drivers/{id}/subscribe - 动态注册订阅特定点位 (实时缓存事件)
ioGroup.MapPost("/subscribe", async (string id, [FromBody] SubscribeAddressRequest request, CommunicationService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Address))
    {
        return Results.BadRequest(new { Message = "Request payload must contain a valid address to subscribe." });
    }

    var success = await service.SubscribeAddressAsync(id, request.Address);
    return success
        ? Results.Ok(new { Message = $"Successfully subscribed to '{request.Address}' on driver '{id}'." })
        : Results.BadRequest(new { Message = $"Failed to register subscription. Ensure driver '{id}' exists and supports subscription." });
})
.WithName("SubscribeAddress")
.WithSummary("动态开启物理点位订阅");

// POST /api/drivers/{id}/unsubscribe - 注销订阅
ioGroup.MapPost("/unsubscribe", async (string id, [FromBody] SubscribeAddressRequest request, CommunicationService service) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.Address))
    {
        return Results.BadRequest(new { Message = "Request payload must contain a valid address." });
    }

    var success = await service.UnsubscribeAddressAsync(id, request.Address);
    return success
        ? Results.Ok(new { Message = $"Successfully unsubscribed from '{request.Address}' on driver '{id}'." })
        : Results.NotFound(new { Message = $"Active subscription for '{request.Address}' on driver '{id}' not found." });
})
.WithName("UnsubscribeAddress")
.WithSummary("取消指定物理点位订阅");

// GET /api/drivers/{id}/subscriptions - 查看该驱动当前所有订阅状态的实时数据缓存
ioGroup.MapGet("/subscriptions", async (string id, CommunicationService service) =>
{
    var list = await service.GetSubscribedValuesAsync(id);
    return Results.Ok(list);
})
.WithName("GetDriverSubscriptions")
.WithSummary("获取当前驱动的所有订阅值");

// GET /api/drivers/{id}/subscriptions/{*address} - 单独获取某项订阅实时状态与最新值
ioGroup.MapGet("/subscriptions/{*address}", async (string id, string address, CommunicationService service) =>
{
    var sub = await service.GetSubscribedValueAsync(id, address);
    return sub != null
        ? Results.Ok(sub)
        : Results.NotFound(new { Message = $"No active subscription cache found for '{address}' on driver '{id}'." });
})
.WithName("GetSingleSubscription")
.WithSummary("查看单条订阅实时缓存");

#endregion

#region 3. ODM 实体对象-设备映射接口组 (Object-Device Mapping & Reflective IO)

var odmGroup = app.MapGroup("/api/odm").WithOpenApi();

// GET /api/odm/schema - 场景 1: 反射扫描实体类型结构，自动提取元数据并生成通讯所需的点位信息
odmGroup.MapGet("/schema", (OdmEngine engine) =>
{
    var tags = engine.GenerateTags<ProductionLineEntity>();
    return Results.Ok(tags);
})
.WithName("GetOdmSchema")
.WithSummary("获取实体对象通讯点位映射架构")
.WithDescription("场景 1: 反射扫描 ProductionLineEntity 结构，获取由自定义 Attribute 标记并解析生成的南向物理点位映射。");

// GET /api/odm/data - 场景 2: 从实体特征中动态提取并激活驱动连接，批量读取设备端数值并自动填充返回实体实例
odmGroup.MapGet("/data", async (OdmEngine engine, CancellationToken ct) =>
{
    try
    {
        var entity = await engine.ReadEntityAsync<ProductionLineEntity>(ct);
        return Results.Ok(entity);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { Message = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { Message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("ReadOdmEntity")
.WithSummary("动态解析实体特征并读取组装数据")
.WithDescription("场景 2: 动态解析实体特性，按需拉起并激活驱动，并反射批量从设备读取值组装为强类型实体。");

// POST /api/odm/data - 场景 2: 反射接收实体数据，强类型自动序列化并批量写入物理点位
odmGroup.MapPost("/data", async ([FromBody] ProductionLineEntity entity, OdmEngine engine, CancellationToken ct) =>
{
    if (entity == null)
    {
        return Results.BadRequest(new { Message = "Payload cannot be null." });
    }

    var response = await engine.WriteEntityAsync(entity, ct);
    return response.Success
        ? Results.Ok(response)
        : Results.Json(response, statusCode: response.StatusCode == 404 ? 404 : 500);
})
.WithName("WriteOdmEntity")
.WithSummary("强类型实体反射写入物理设备")
.WithDescription("场景 2: 接收强类型实体，反射获取属性值，自动强类型转换并批量物理下发写入。");

#endregion

#region 4. 任务调度系统接口组 (Job Scheduler Management)

var jobsGroup = app.MapGroup("/api/jobs").WithOpenApi();

// GET /api/jobs/count - 获取当前正在执行的任务数量
jobsGroup.MapGet("/executing-count", async (JobScheduler scheduler) =>
{
    var count = await scheduler.GetExecutingJobsCountAsync();
    return Results.Ok(new { ExecutingCount = count });
})
.WithName("GetExecutingJobsCount")
.WithSummary("获取当前正在执行的 Job 数量")
.WithDescription("实时统计整个调度器中当前正处于运行状态 the Job 总数。");

// GET /api/jobs - 查询所有已调度任务列表
jobsGroup.MapGet("/", async (JobScheduler scheduler) =>
{
    var list = await scheduler.GetJobsAsync();
    return Results.Ok(list);
})
.WithName("GetJobsList")
.WithSummary("获取所有任务调度列表")
.WithDescription("返回调度器中所有已经注册和安排的任务的元数据、状态及数据映射。");

// GET /api/jobs/{id} - 查询单个任务的详细信息
jobsGroup.MapGet("/{id}", async (string id, JobScheduler scheduler) =>
{
    var job = await scheduler.GetJobAsync(id);
    return job != null ? Results.Ok(job) : Results.NotFound(new { Message = $"Job '{id}' not found." });
})
.WithName("GetJobDetails")
.WithSummary("查询单个任务调度详情");

// POST /api/jobs - 动态安排新任务
jobsGroup.MapPost("/", async ([FromBody] CreateJobRequest request, JobScheduler scheduler) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.JobId) || string.IsNullOrWhiteSpace(request.JobType) || string.IsNullOrWhiteSpace(request.CronExpression))
    {
        return Results.BadRequest(new { Message = "Required parameters: JobId, JobType, and CronExpression must be provided." });
    }

    var resolvedType = AppDomain.CurrentDomain.GetAssemblies()
        .SelectMany(a => a.GetTypes())
        .FirstOrDefault(t => (t.Name == request.JobType || t.FullName == request.JobType) && typeof(IJob).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

    if (resolvedType == null)
    {
        return Results.BadRequest(new { Message = $"Job type '{request.JobType}' was not found or does not implement IJob." });
    }

    try
    {
        var dataMap = new JobDataMap();
        if (request.JobDataMap != null)
        {
            foreach (var kv in request.JobDataMap)
            {
                dataMap[kv.Key] = kv.Value;
            }
        }

        await scheduler.ScheduleJobAsync(request.JobId, resolvedType, request.CronExpression, dataMap);
        return Results.Created($"/api/jobs/{request.JobId}", new { Message = $"Job '{request.JobId}' scheduled successfully." });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { Message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("CreateJob")
.WithSummary("动态调度新任务");

// PUT /api/jobs/{id} - 更新已安排的任务 (Cron 及伴随数据)
jobsGroup.MapPut("/{id}", async (string id, [FromBody] UpdateJobRequest request, JobScheduler scheduler) =>
{
    if (request == null || string.IsNullOrWhiteSpace(request.CronExpression))
    {
        return Results.BadRequest(new { Message = "Required parameter: CronExpression must be provided." });
    }

    try
    {
        var dataMap = request.JobDataMap != null ? new JobDataMap((IDictionary<string, object>)request.JobDataMap) : null;
        var success = await scheduler.UpdateJobAsync(id, request.CronExpression, dataMap);
        return success
            ? Results.Ok(new { Message = $"Job '{id}' updated successfully." })
            : Results.NotFound(new { Message = $"Job '{id}' not found." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("UpdateJob")
.WithSummary("修改已安排任务");

// DELETE /api/jobs/{id} - 注销并彻底删除特定任务
jobsGroup.MapDelete("/{id}", async (string id, JobScheduler scheduler) =>
{
    try
    {
        var success = await scheduler.DeleteJobAsync(id);
        return success
            ? Results.Ok(new { Message = $"Job '{id}' deleted successfully." })
            : Results.NotFound(new { Message = $"Job '{id}' not found." });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message, statusCode: 500);
    }
})
.WithName("DeleteJob")
.WithSummary("注销删除指定任务");

#endregion

app.Run();
