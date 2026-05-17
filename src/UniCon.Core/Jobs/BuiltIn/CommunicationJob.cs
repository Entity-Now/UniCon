using Microsoft.Extensions.Logging;
using Quartz;
using UniCon.Core;
using UniCon.Core.Models;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace UniCon.Core.Jobs.BuiltIn
{
    public class CommunicationJob : UniConJobBase
    {
        private readonly IDriverRegistry _driverRegistry;

        public CommunicationJob(ILogger<CommunicationJob> logger, IDriverRegistry driverRegistry) : base(logger)
        {
            _driverRegistry = driverRegistry;
        }

        public override async Task Execute(IJobExecutionContext context)
        {
            var dataMap = context.MergedJobDataMap;
            var driverId = GetStringSafe(dataMap, JobDataKeys.CommDriverId);
            var address = GetStringSafe(dataMap, JobDataKeys.CommAddress);
            var operation = GetStringSafe(dataMap, JobDataKeys.CommOperation) ?? "Read";
            var value = dataMap.ContainsKey(JobDataKeys.CommValue) ? dataMap.Get(JobDataKeys.CommValue) : null;
            var dataTypeStr = GetStringSafe(dataMap, JobDataKeys.CommDataType) ?? "System.Object";

            if (string.IsNullOrEmpty(driverId) || string.IsNullOrEmpty(address)) return;

            var driver = _driverRegistry.Get(driverId);
            if (driver == null || !driver.IsConnected) return;

            try
            {
                Type dataType = Type.GetType(dataTypeStr) ?? typeof(object);
                var request = new UniconRequest { Address = address };

                // 使用 Quartz 提供的 CancellationToken
                var ct = context.CancellationToken;

                if (operation.Equals("Write", StringComparison.OrdinalIgnoreCase))
                {
                    await ExecuteWriteAsync(driver, request, value, dataType, ct);
                }
                else
                {
                    await ExecuteReadAsync(driver, request, dataType, ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CommunicationJob failed for {driverId}");
            }
        }

        private string? GetStringSafe(JobDataMap map, string key) => map.ContainsKey(key) ? map.GetString(key) : null;

        private async Task ExecuteReadAsync(IUniconDriver driver, UniconRequest request, Type dataType, CancellationToken ct)
        {
            var method = driver.GetType().GetMethod("ReadAsync")?.MakeGenericMethod(dataType);
            if (method != null)
            {
                var task = (Task)method.Invoke(driver, new object[] { request, ct })!;
                await task;
                var resultProperty = task.GetType().GetProperty("Result");
                var response = resultProperty?.GetValue(task);
                _logger.LogInformation($"Read Response: {response}");
            }
        }

        private async Task ExecuteWriteAsync(IUniconDriver driver, UniconRequest request, object? value, Type dataType, CancellationToken ct)
        {
            var method = driver.GetType().GetMethod("WriteAsync")?.MakeGenericMethod(dataType);
            if (method != null)
            {
                var convertedValue = value != null ? Convert.ChangeType(value, dataType) : null;
                var task = (Task)method.Invoke(driver, new object[] { request, convertedValue!, ct })!;
                await task;
                var resultProperty = task.GetType().GetProperty("Result");
                var response = resultProperty?.GetValue(task);
                _logger.LogInformation($"Write Response: {response}");
            }
        }
    }
}
