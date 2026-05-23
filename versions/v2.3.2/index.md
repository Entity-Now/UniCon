---
url: /versions/v2.3.2/index.md
---
# v2.3.2 — 修复 VuePress 文档构建编译错误 (v2.3.2)

**日期**：2026-05-23
**类型**：Fix / Docs

***

## \[v2.3.2] - 2026-05-23

### Fixed

* **VuePress Compiling**: Fixed HTML tag tokenization and parsing syntax errors caused by C# generic parameters (such as `DataValue<object>`, `DataValue<T>`, `Channel<T>`) in markdown files by converting them to double-escaped HTML entities with standard `<code>` blocks, completely bypassing the Vue template compiler's element parsing rules.

### Key Changes

* Modified `.vuepress_docs/docs/versions/v2.0.0.md` to escape C# generics in inline text and code.
* Modified `.vuepress_docs/docs/docs/5. webapi/2.driver-endpoints.md` to escape C# generics.
* Modified `.vuepress_docs/docs/docs/3. drivers/2.opcua-pubsub.md` to escape C# generics.
* Modified `.vuepress_docs/docs/docs/2. core/7.driver-architecture-v2.md` to escape C# generics.
* Modified `.vuepress_docs/docs/docs/2. core/6.active-polling-subscription.md` to escape C# generics.
* Modified `.vuepress_docs/docs/docs/2. core/1.intro.md` to escape C# generics.
* Modified `.vuepress_docs/docs/versions/v1.5.0.md` to escape C# generics.

***

\------details-----

## 🔍 Task Details

## 📌 Current Task Board: 修复 VuePress 文档构建编译错误

### 🎯 最终目标

* 修复 `v2.0.0.md` 及其他 Markdown 文件中由于 C# 泛型参数 `<object>` 导致 VuePress (Vite/Vue) 编译报错 "Element is missing end tag" 的问题。
* 确保 `pnpm docs:build` 成功通过。

### 📂 涉及文件 (Strictly Locked)

* \[x] [v2.3.2.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/versions/v2.3.2.md) (原因：创建版本记录文件)
* \[x] [v2.0.0.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/versions/v2.0.0.md) (原因：修复 line 31, 50, 54, 59, 82 中的 `<object>` / `<T>` 标签解析错误)
* \[x] [6.active-polling-subscription.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs/2.%20core/6.active-polling-subscription.md) (原因：修复潜在的 `<object>` 解析错误)
* \[x] [2.opcua-pubsub.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs/3.%20drivers/2.opcua-pubsub.md) (原因 ：修复潜在的 `<object>` 解析错误)
* \[x] [2.driver-endpoints.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs/5.%20webapi/2.driver-endpoints.md) (原因：修复潜在的 `<object>` 解析错误)
* \[x] [7.driver-architecture-v2.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs/2.%20core/7.driver-architecture-v2.md) (原因：修复潜在的 `<object>` 解析错误)
* \[x] [1.intro.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/docs/2.%20core/1.intro.md) (原因：修复潜在的 `<T>` 解析错误)
* \[x] [v1.5.0.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/versions/v1.5.0.md) (原因：修复潜在的 `<T>` 解析错误)

### 📝 Steps

* \[x] Step 1: 创建 `v2.3.2.md` 版本记录文件并锁定上下文。
* \[x] Step 2: 修复 `v2.0.0.md` 中导致编译失败的 `<object>` 泛型语法。
* \[x] Step 3: 检查并修复其他可能受影响的 `.md` 文档中的 `<object>` / `<T>` 泛型语法。
* \[x] Step 4: 运行 `pnpm docs:build` 验证文档构建是否成功通过。
* \[x] Step 5: 更新 `v2.3.2.md` 状态并归档。

\------details end------
