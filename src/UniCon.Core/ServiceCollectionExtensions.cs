using Microsoft.Extensions.DependencyInjection;
using UniCon.Core.Caching;
using UniCon.Core.Odm;

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

            // 核心数据对象-设备映射引擎 (ODM Engine)
            services.AddSingleton<OdmEngine>();

            // 统一缓存提供者 (Memory 默认实现，可由外部容器重写/覆盖为 Redis) (RULE 2.2)
            services.AddSingleton<IUniconCacheProvider, MemoryCacheProvider>();

            return services;
        }
    }
}
