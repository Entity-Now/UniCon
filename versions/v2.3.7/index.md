---
url: /versions/v2.3.7/index.md
---
# v2.3.7 — 配置 NuGet 打包属性与 README 支持 (v2.3.7)

**日期**：2026-05-23
**类型**：CI/CD / MSBuild

***

## \[v2.3.7] - 2026-05-23

### Added / Changed

* **MSBuild / NuGet**: Created a global `Directory.Build.props` configuration at the repository root to automatically inject authors, email, website url metadata, and the root `README.md` file as the official NuGet Package README into all packaged library assemblies when executing `dotnet pack`.
* **Project Configuration**: Added `<IsPackable>false</IsPackable>` to `UniCon.WebServer.csproj` to prevent executable servers from being incorrectly packaged as NuGet libraries.

### Key Changes

* Created `Directory.Build.props` with metadata fields (`entity-now`, `entity_now@qq.com`, `https://unicon.rdr2.cn`).
* Configured MSBuild to embed the root `README.md` in every generated `.nupkg` package.
* Disabled packaging for the web service host.

***

\------details-----

## 🔍 Task Details

## 📌 Current Task Board: 配置 NuGet 打包属性与 README 支持

### 🎯 最终目标

* 在所有打包的 NuGet 类库项目中，自动注入指定的作者、邮箱、官网元数据，并集成项目主 `README.md` 作为 NuGet 展示首页。

### 📂 涉及文件 (Strictly Locked)

* \[x] [Directory.Build.props](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/Directory.Build.props) (原因：新建全局 MSBuild 属性配置，统筹元数据与 README 打包)
* \[x] [src/UniCon.WebServer/UniCon.WebServer.csproj](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/src/UniCon.WebServer/UniCon.WebServer.csproj) (原因：禁用表现层 Web 服务类库打包)
* \[x] [.vuepress\_docs/docs/versions/v2.3.7.md](file:///Users/entity/Desktop/Language/CSharp/UniGateway/UniCon/.vuepress_docs/docs/versions/v2.3.7.md) (原因：创建并管理版本记录文件)

### 📝 Steps

* \[x] Step 1: 创建 `v2.3.7.md` 并锁定上下文与 Task Board
* \[x] Step 2: 创建全局 `Directory.Build.props`，定义作者、邮箱、项目 URL 以及集成 `README.md` 的 MSBuild 属性
* \[x] Step 3: 更新 `UniCon.WebServer.csproj`，显示设置 `<IsPackable>false</IsPackable>`
* \[x] Step 4: 更新 Task Board 并归档为 Release 记录

\------details end------
