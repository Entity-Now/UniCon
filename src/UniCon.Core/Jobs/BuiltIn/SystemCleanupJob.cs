using Microsoft.Extensions.Logging;
using Quartz;

namespace UniCon.Core.Jobs.BuiltIn
{
    /// <summary>
    /// 内置任务：系统清理（示例）
    /// </summary>
    public class SystemCleanupJob : UniConJobBase
    {
        public SystemCleanupJob(ILogger<SystemCleanupJob> logger) : base(logger)
        {
        }

        public override async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation("Executing SystemCleanupJob...");

            // 执行清理逻辑 (遵循 RULE 2.1 保持逻辑清晰)
            await PerformCleanupAsync();

            _logger.LogInformation("SystemCleanupJob completed.");
        }

        private async Task PerformCleanupAsync()
        {
            // 模拟清理逻辑
            await Task.Delay(100);
        }
    }
}
