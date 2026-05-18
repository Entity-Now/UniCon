using System.Collections.Generic;

namespace UniCon.WebServer.Models
{
    /// <summary>
    /// 创建调度任务的请求模型
    /// </summary>
    /// <param name="JobId">任务唯一标识</param>
    /// <param name="JobType">任务类型简称或全称 (例如 HttpJob, SystemCleanupJob, CommunicationJob)</param>
    /// <param name="CronExpression">CRON 表达式</param>
    /// <param name="JobDataMap">伴随任务运行的数据映射</param>
    public record CreateJobRequest(
        string JobId,
        string JobType,
        string CronExpression,
        Dictionary<string, object>? JobDataMap
    );

    /// <summary>
    /// 更新调度任务的请求模型
    /// </summary>
    /// <param name="CronExpression">新 CRON 表达式</param>
    /// <param name="JobDataMap">新的伴随任务运行的数据映射 (可选)</param>
    public record UpdateJobRequest(
        string CronExpression,
        Dictionary<string, object>? JobDataMap
    );
}
