using System.Collections.Generic;

namespace UniCon.Core.Jobs
{
    /// <summary>
    /// Job 调度详细信息模型
    /// </summary>
    /// <param name="JobId">任务唯一标识</param>
    /// <param name="JobType">任务具体实现类型全称</param>
    /// <param name="CronExpression">CRON 表达式</param>
    /// <param name="Status">当前触发器状态 (例如 Normal, Paused, Complete, Error 等)</param>
    /// <param name="JobData">伴随任务运行的数据映射</param>
    public record JobInfo(
        string JobId,
        string JobType,
        string CronExpression,
        string Status,
        IDictionary<string, object> JobData
    );
}
