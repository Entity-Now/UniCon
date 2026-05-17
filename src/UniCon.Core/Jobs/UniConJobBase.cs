using Microsoft.Extensions.Logging;
using Quartz;

namespace UniCon.Core.Jobs
{
    /// <summary>
    /// UniCon 任务基类
    /// </summary>
    public abstract class UniConJobBase : IJob
    {
        protected readonly ILogger _logger;

        protected UniConJobBase(ILogger logger)
        {
            _logger = logger;
        }

        public abstract Task Execute(IJobExecutionContext context);
    }
}
