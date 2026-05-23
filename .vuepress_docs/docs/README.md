---
pageLayout: home
externalLinkIcon: false
config:
  -
    type: hero
    full: true
    background: tint-plate
    forceDark: true
    effect: lightning
    hero:
      name: UniCon
      tagline: 高性能、插件化的 .NET 10 工业物联网通讯集成框架
      text: 屏蔽物理链路与底层协议差异，提供统一读写契约与极速 Channel 扫描引擎，为工业 4.0 与物联网提供强力支撑。
      actions:
        -
          theme: brand
          text: 快速开始 →
          link: /docs/1. introduce/1.intro.md
        -
          theme: alt
          text: 更新记录
          link: /versions/
        -
          theme: alt
          text: GitHub →
          link: https://github.com/entity/UniCon
  -
    type: features
    features:
      -
        icon: 🛡️
        title: 统一读写契约
        details: 借鉴类 Web 契约，引入 UniconRequest/Response 抽象，统一屏蔽 PLC、时序库及云端协议的物理差异。
      -
        icon: ⚡
        title: v2 极速扫描引擎
        details: 基于 System.Threading.Channels 的异步无锁队列与分发，极致降低高并发大规模轮询下的资源消耗。
      -
        icon: 🔄
        title: 智能自愈守卫
        details: 内置 Watchdog 监控和指数退避重连算法，确保在恶劣的工业现场环境下仍能实现通讯自愈。
      -
        icon: 🕒
        title: 工业级定时调度
        details: 深度集成 Quartz.NET，支持 CRON 表达式及设备状态联动的复杂定时任务链和批处理调度。
      -
        icon: 🌐
        title: 动态 Minimal API
        details: 提供开箱即用的 WebAPI，支持在运行时动态注册/配置驱动，直接暴露标准 RESTful 数据接口。
      -
        icon: 📊
        title: 质量与时序溯源
        details: DataValue<T> 带有 Quality 数据质量戳和设备端 Server Timestamp，全面支持时序数据库清洗审计要求。
---
