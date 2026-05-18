using Microsoft.Extensions.DependencyInjection;
using UniCon.Core.Caching;
using UniCon.Core.Odm;
using UniCon.Core.Jobs;
using UniCon.Core.Network;
using Quartz;
using System.Linq;

namespace UniCon.Core
{
    /// <summary>
    /// 提供面向 IServiceCollection 的扩展方法以支持 NuGet 一键集成 (IoC, DI)
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 一键向 DI 容器中注入 UniCon 核心框架组件（单例注册，全局全局唯一活动物理连接）
        /// </summary>
        public static IServiceCollection AddUniCon(this IServiceCollection services)
        {
            // 核心驱动全局注册中心
            services.AddSingleton<IDriverRegistry, DriverRegistry>();

            // 核心物理连接自愈管理器
            services.AddSingleton<IConnectionManager, ConnectionManager>();

            // 全局硬件级网络可用性监测引擎
            services.AddSingleton<INetworkMonitor, NetworkMonitor>();

            // 核心数据对象-设备映射引擎 (ODM Engine)
            services.AddSingleton<OdmEngine>();

            // 统一缓存提供者 (Memory 默认实现，可由外部容器重写/覆盖为 Redis) (RULE 2.2)
            services.AddSingleton<IUniconCacheProvider, MemoryCacheProvider>();

            return services;
        }

        /// <summary>
        /// 一键向 DI 容器中注入 UniCon 任务调度系统，并自动扫描并注册内置 IJob 任务
        /// </summary>
        public static IServiceCollection AddUniConJobs(this IServiceCollection services)
        {
            // 注册 Quartz.NET 核心服务
            services.AddQuartz(q =>
            {
                // 使用 Microsoft DI 托管 Job 实例化，支持依赖注入
                q.UseMicrosoftDependencyInjectionJobFactory();
            });

            // 注册 Quartz.NET 托管宿主服务
            services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

            // 注册单例 JobScheduler 控制器
            services.AddSingleton<JobScheduler>();

            // 自动扫描并注册当前程序集中所有非抽象的 IJob 实现类为瞬时服务，以确保依赖注入能够正常解析
            var jobTypes = typeof(ServiceCollectionExtensions).Assembly.GetTypes()
                .Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var jobType in jobTypes)
            {
                services.AddTransient(jobType);
            }

            return services;
        }
    }
}

