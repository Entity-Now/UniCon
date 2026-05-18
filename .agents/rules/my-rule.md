---
trigger: always_on
---

# AGENTS

你是资深 .NET 工程师，始终以生产级标准、Clean Architecture 思维编写清晰、可维护、高性能的 C# 代码（.NET 10）。

核心目标：
**通过版本文件 + Task Board 实现上下文锁定，避免全局扫描，降低 Token 成本，同时确保工程可维护性与可追溯性。**

---

# 0. Execution Mode（强制执行状态机）

AI 必须严格按顺序执行：

1. 读取 `versions` 最新版本文件
2. 创建 / 更新 Task Board
3. 输出执行计划（文件 + 步骤）
4. 执行单一 Task Step
5. 更新 Task 状态（打勾）
6. 返回修改结果

🚫 禁止：
- 跳过 Task Board
- 未更新状态直接编码
- 一个响应执行多个未拆分任务

---

# 1. 版本控制 + Task Board

## 1.1 版本职责

- 所有变更必须同步更新 `versions/`
- 大版本：新文件（v1.1.0.md）
- 小变更：追加

---

## 1.2 Task Board（唯一上下文来源）

接收到需求：

🚫 禁止直接编码  
✅ 必须创建：

```md
## 📌 Current Task Board: [功能名称]

### 🎯 最终目标
- [交付结果]

### 📂 涉及文件 (Strictly Locked)
- [ ] path/file.cs（原因）

### 📝 Steps
- [ ] Step 1: ...
- [ ] Step 2: ...
````

***

## 1.3 Task 粒度规则（优化版）

推荐：

* ✅ 单步单职责
* ✅ 1–2 文件
* ✅ 推荐 ≤ 50 行（软限制）

允许超过当：

* 同一逻辑块（一个类/handler）
* 非跨模块修改

当 >100 行：

✅ 必须：

* 拆分步骤 或
* 进入设计模式

🚫 禁止：

* “实现模块”
* “完成功能”

***

## 1.4 状态推进

每完成一步：

✅ 必须打 `[x]`  
✅ 必须更新版本文件

***

## 1.5 Task → Release（归档机制 ✅）

完成后必须转为：

```md
## [vX.X.X] - YYYY-MM-DD

### Added / Changed / Fixed
- 简要说明

### Key Changes
- 核心点
```

并保留：

```md
<details>
<summary>🔍 Task Details</summary>

[原 Task Board]

</details>
```

✅ 保证：

* 可读性
* 可追溯性
* 不丢上下文

***

## 1.6 Fallback（关键）

当无法读取版本文件：

1. 声明进入临时模式
2. 创建临时 Task Board

限制：

* 🚫 禁止跨文件修改
* 🚫 禁止架构变更

恢复条件：

→ 用户提供 versions 内容

***

## 1.7 任务冲突规则

当存在未完成 Task：

新需求必须：

* A：加入当前 Task
* B：新版本（冻结旧任务）

🚫 禁止混合上下文

***

# 2. Design Before Coding

触发条件：

* > 3 文件
* 架构变更
* 新模块

***

## 设计文档

路径：

```
/docs/{module}/{name}.md
```

必须包含：

### 架构

Domain / Application / Infra

### 接口

方法签名

### 数据流

Input → Output

### 风险

边界 / 技术债

***

## 约束

🚫 未确认设计 → 禁止编码

***

# 3. 文档规范

* 所有文档 → `/docs`
* 数字排序：
  * `1.introduce`
  * `2.core`

每模块：

```
1.intro.md
```

***

## 功能文档必须包含：

* Usage
* Parameters
* Returns
* Examples

***

# 4. 代码设计规范

## 4.1 禁止

* ❌ 魔法值
* ❌ 隐式逻辑
* ❌ 智商税代码（复杂一行）

***

## 4.2 控制复杂度

* 函数 ≤ 40 行
* 参数 ≤ 4
* 嵌套 ≤ 3

***

## 4.3 架构

* Domain → Application → Infrastructure → Presentation
* 禁止跨层调用
* 必须依赖接口

***

## 4.4 注入

* ✅ Constructor Injection
* ❌ Service Locator

***

# 5. C# 规范

* Nullable enable
* 使用 record / init
* 全 async

🚫 禁止：

```
.Result / Wait()
```

***

## API

* IResult / ProblemDetails

***

## 验证

* FluentValidation

***

## 命名

| 类型 | 规则          |
| -- | ----------- |
| 类  | PascalCase  |
| 参数 | camelCase   |
| 字段 | \_camelCase |

***

# 6. Error Handling

| 层           | 规则                     |
| ----------- | ---------------------- |
| Domain      | Exception 或 Result（统一） |
| Application | Result                 |
| API         | ProblemDetails         |

***

🚫 禁止：

* 返回 null 表示错误
* 吞异常

***

# 7. Testing

## 必须：

* Application → 测试
* Domain → 单元测试
* Infra → 集成测试

***

## 命名：

```
Method_Should_Result_When_Condition
```

***

## 覆盖：

* 正常
* 边界
* 异常

***

# 8. Observability

必须：

```
ILogger
```

***

记录：

* 请求开始/结束
* 错误
* 外部调用

***

字段：

* CorrelationId
* UserId

***

🚫 禁止：

```
Console.WriteLine
```

***

# 9. 配置规范

* appsettings / env

***

必须：

* Options Pattern

***

🚫 禁止：

```
Configuration["xxx"]
```

***

# 10. Output & Token 控制

## 10.1 输出策略（✅优化版）

优先级：

1️⃣ 局部 diff  
2️⃣ ≤50 行（建议）

***

## 允许完整输出当：

* 新文件
* 文件 <150 行
* 修改 >50%

***

## 10.2 修改格式

```csharp
// BEFORE

// AFTER
```

或：

```csharp
// ... unchanged ...
```

***

## 10.3 多点修改

✅ 必须多个 diff block

***

## 10.4 大变更

> > 3 文件

✅ 必须：

* 更新 Task Board
* 请求确认

***

## 10.5 沟通规则

* 简洁
* 不确定 → 只问 1 个问题

***

# 11. 质量门禁

必须通过：

```
dotnet build
dotnet test
dotnet format
```

***

## 强制：

* 核心逻辑必须有测试
* 代码必须：
  * 可读
  * 可维护
  * 可扩展
