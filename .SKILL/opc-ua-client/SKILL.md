这是一份针对你提供的 opc-ua-client 使用案例整理而成的 AI Skill 文档。它旨在让 AI（或开发者）快速理解该类的核心逻辑、依赖关系以及如何调用它进行 OPC UA 通信。
Skill: OPC UA Client (基于 opc-ua-client 库)
1. 概述 (Overview)
该 Skill 定义了一个名为 WDS_OPC 的类，封装了基于 opc-ua-client 库的 OPC UA 通信逻辑。它支持网络自检、多种身份验证方式（匿名、账号密码、X509 证书）、自动重连机制以及异步的读写操作。
2. 核心架构与依赖 (Architecture & Dependencies)
• 库依赖: opc-ua-client (核心通信), BouncyCastle (证书解析), Microsoft.Extensions.Configuration (配置加载)。
• 关键组件: • ClientSessionChannel: 维持与服务器的会话通道。 • OPC_Option: 存储连接配置（URL、安全策略、凭据）。 • NetworkHelper: (外部工具类) 用于 Ping 和 TCP 端口检测。
3. 关键功能逻辑 (Key Functions)
A. 身份验证流程
支持三种模式，通过 OPC_Option 切换：
1. 匿名 (Anonymous): 无需凭据。
2. 账号密码 (UserName): 传入用户名与密码。
3. 证书 (X509): 从指定路径 (/pki/own/certs/public.der 和 /pki/own/private/private.pem) 读取并解析 X509 证书。
B. 连接生命周期管理
• 延迟初始化: 只有在网络检查 (CheckNetwork) 通过后才会尝试创建连接。
• 状态机控制: 通过 CommunicationState (Created, Opening, Opened, Faulted) 管理连接流程。
• 异常重连: 订阅了 Faulted 事件，当连接中断时自动调用 Reconnect() 重新构建通道。
C. 数据操作 (Read/Write)
• 批量读取: 接受 List<string> 形式的 NodeId，返回 ReadResponse。
• 批量写入: 接受 Dictionary<string, object> (NodeId -> Value)，将值封装进 Variant 进行写入。
4. API 规范 (API Specification)
初始化与连接
方法	说明
WDS_OPC(...)	构造函数，绑定配置并尝试初始化连接。
Task OpenAsync()	异步确保连接处于 Open 状态（含状态轮询和递归重试）。
bool CheckNetwork()	预检：Ping IP 并测试 TCP 端口连通性。
数据交互
方法	参数	返回值
GetNodeValueAsync	List<string> nodes	Task<ReadResponse>
SetNodeValueAsync	Dictionary<string, object> nodes	Task<WriteResponse>
5. 最佳实践与注意事项 (Best Practices)
1. 单例建议: 类中使用了 opcLook 静态锁，且通道管理具有重连机制，建议在应用中作为单例使用。
2. 证书路径: 默认证书存储在执行目录下的 pki 文件夹中。若启用安全策略，AI 需确保对应文件存在。
3. 线程安全: 读写方法内部均调用了 await OpenAsync()，这保证了在执行操作前连接一定是可用的。
4. 资源释放: 析构函数中包含 AbortAsync 和 CloseAsync，确保程序退出时正常断开。
6. 使用示例 (Usage Example)
// 1. 初始化
var opcService = new WDS_OPC(logger, configuration);

// 2. 读取数据
var nodesToRead = new List<string> { "ns=2;s=Device1.Temperature", "ns=2;s=Device1.Status" };
var readResult = await opcService.GetNodeValueAsync(nodesToRead);

// 3. 写入数据
var nodesToWrite = new Dictionary<string, object> 
{ 
    { "ns=2;s=Device1.ControlWord", (short)1 } 
};
await opcService.SetNodeValueAsync(nodesToWrite);

Status: Ready to use in .NET 6/7/8+ environments.