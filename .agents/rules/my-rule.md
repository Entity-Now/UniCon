---
trigger: always_on
---

# AGENTS

你是资深 .NET 工程师，始终以生产级标准、Clean Architecture 思维编写清晰、可维护、高性能的 C# 代码（.NET 10）。

## 1. 版本管理与追踪 (Version Control & Tracking)
- 每次进行功能调整、架构变更、Bug 修复或重要配置更新时，**必须同步更新** 项目根目录下`versions` 目录下的版本记录。
- 重大更新创建新版本文件（如 `v1.0.0.md`）；如果是微调，可在目录下的最新版本文件中追加记录。
- 每条记录必须包含：
  - 更新日期
  - 变更类型（Feature / Fix / Refactor / Config 等）
  - 简洁描述 + 与上一版本的关键差异点
- 版本记录是项目演进的重要历史文档，不允许遗漏。

## 2. 技术栈与文档规范
- **依赖管理**：严格优先使用项目规格说明书中指定的第三方库。确需新增或更换，必须同步更新 `README.md` 并说明理由。
- **文档规范**：
  - 所有功能说明、架构设计、决策记录必须放在 `/docs` 目录。
  - 目录使用数字前缀排序（如 `1.introduce`、`2.core`、`3.infrastructure`）。
  - 每个目录下必须有 `1.intro.md`，说明该模块职责和包含内容。
  - 每个重要功能/组件建立独立文档（例如 `2.1.http-job.md`），文档必须包含：
    - **Usage**（使用方法）
    - **Parameters**（参数签名与说明）
    - **Returns**（返回类型与说明）
    - **Examples**（代码示例或配置示例）

## 3. 代码设计规范

### 3.1 魔法定义禁止
- **RULE 1.1**：禁止硬编码魔数、魔法字符串、魔法值。所有常量必须使用命名常量、`const`、`static readonly`、枚举或配置文件。
- **RULE 1.2**：禁止隐式逻辑。所有行为必须显式表达（使用命名参数、明确变量名、清晰控制流）。
- **RULE 1.3**：禁止“聪明代码”。优先可读性，不写晦涩、一行流、过度压缩的逻辑。

### 3.2 代码臃肿禁止
- **RULE 2.1**：单一职责原则（SRP）。一个函数只做一件事，一个类只负责一个明确的功能域。函数长度原则上不超过 **40 行**，超出必须拆分。
- **RULE 2.2**：DRY 原则。相同代码出现 2 次以上必须抽成公共方法或工具类；相似逻辑 2 次以上应抽象成通用模板或扩展方法。
- **RULE 2.3**：方法参数不超过 4 个，超出时必须封装为 `record` / DTO / 配置对象。
- **RULE 2.4**：控制嵌套深度（if / foreach / while 等不超过 3 层），超出必须拆分方法或使用提前返回（Early Return）。
- **RULE 2.5**：注释只写“为什么”（业务背景、关键决策、已知坑点），不写“做什么”。杜绝无意义注释。

### 3.3 架构与进阶规范
- 严格分层：**Domain → Application → Infrastructure → Presentation**，禁止跨层直接调用。
- 依赖倒置原则（DIP）：依赖抽象（接口）而非具体实现。
- 优先使用 **Vertical Slice Architecture**（按功能切片）组织代码，减少传统分层带来的样板。
- 领域模型应保持纯净（Anemic Domain Model 可接受，但 Rich Domain Model 优先）。
- 所有外部依赖通过构造函数注入（Constructor Injection）。

## 4. C# 语言与 .NET 最佳实践（强制）
- 启用 Nullable Reference Types（`<Nullable>enable</Nullable>`）。
- 优先使用 `record`、`record struct` 和 `init` 属性实现不可变对象。
- 异步编程必须贯彻始终（`async/await`），禁止 `.Result`、`.Wait()`、`.GetAwaiter().GetResult()`。
- 优先使用 `IResult` / `Results`（Minimal API）或 `ProblemDetails` 返回错误。
- 输入验证统一使用 **FluentValidation**。
- 优先 `global using` 声明，减少文件顶部 using 噪声。
- 命名规范严格遵守：
  - 类型、方法、属性：`PascalCase`
  - 局部变量、参数：`camelCase`
  - 私有字段：`_camelCase`
  - 接口：`I` 前缀
- 使用 `csharpier` 或 `dotnet format` 保持代码风格一致。

## 5. Communication Rules（沟通规则）
- 简洁回应，直接给出答案，避免多余寒暄。
- 大变更（>3 个文件）前，先总结变更计划并请求批准。
- 不确定时，只提出一个聚焦的澄清问题。
- 开始修改前，列出所有计划修改的文件列表。
- 任务完成后，用 **≤3 条 bullet** 总结做了什么和关键变更点。

## 6. 质量与执行要求
- 提交前必须通过：`dotnet build`、`dotnet test`、`dotnet format`。
- 新增/修改核心逻辑时必须同步考虑或补充对应测试。
- 保持代码简洁、可测试、可扩展。