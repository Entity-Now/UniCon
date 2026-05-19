#!/bin/bash
set -e

REPO_DIR="/Users/entity/Desktop/Language/CSharp/UniGateway/UniCon"
TEMP_DIR="/Users/entity/Desktop/Language/CSharp/UniGateway/unicon-docs-temp"

echo "Initializing docs branch in temp directory..."
rm -rf "$TEMP_DIR"
mkdir -p "$TEMP_DIR"

# Clone the local repository to temp
git clone "$REPO_DIR" "$TEMP_DIR"

cd "$TEMP_DIR"

# Checkout to orphan docs branch
git checkout --orphan docs
git rm -rf --cached .
git clean -fdx

# Create VuePress Theme Plume files
cat << 'EOF' > package.json
{
  "name": "unicon-docs",
  "version": "1.0.0",
  "description": "UniCon Documentation",
  "scripts": {
    "dev": "vuepress dev docs",
    "build": "vuepress build docs"
  },
  "dependencies": {
    "vue": "^3.4.0"
  },
  "devDependencies": {
    "@vuepress/bundler-vite": "2.0.0-rc.30",
    "vuepress": "2.0.0-rc.30",
    "vuepress-theme-plume": "1.0.0-rc.201"
  }
}
EOF

cat << 'EOF' > .gitignore
.vuepress/.cache
.vuepress/.temp
.vuepress/dist
node_modules
.DS_Store
EOF

mkdir -p docs/.vuepress

cat << 'EOF' > docs/.vuepress/config.ts
import { viteBundler } from '@vuepress/bundler-vite'
import { defineUserConfig } from 'vuepress'
import { plumeTheme } from 'vuepress-theme-plume'

export default defineUserConfig({
  lang: 'zh-CN',
  title: 'UniCon',
  description: 'Clean Architecture 思维编写的生产级高性能 .NET 10 通信网关框架',

  bundler: viteBundler(),

  theme: plumeTheme({
    autoFrontmatter: {
      permalink: 'filepath',
      createTime: true,
      title: true,
    },
    search: {
      provider: 'local',
    },
    markdown: {
      hint: true,
      alert: true,
      fileTree: true,
      plot: true,
      icons: true,
      math: true,
      mermaid: true,
      echarts: true,
    },
    readingTime: {},
  }),
})
EOF

cat << 'EOF' > docs/.vuepress/plume.config.ts
import { defineThemeConfig, defineCollection } from 'vuepress-theme-plume'

export default defineThemeConfig({
  logo: 'https://img.shields.io/badge/UniCon-Gateway-blue?style=flat-square',
  profile: {
    avatar: 'https://avatars.githubusercontent.com/u/100000000?v=4',
    name: 'UniCon Team',
    description: '工业网关统一通讯框架',
    circle: true,
    location: 'China',
  },
  social: [
    { icon: 'github', link: 'git@github.com:Entity-Now/UniCon.git' }
  ],
  navbar: [
    { text: '介绍', link: '/introduce/intro.html', icon: 'material-symbols:explore-outline' },
    { text: '核心模块', link: '/core/intro.html', icon: 'material-symbols:architecture-outline' },
    { text: '驱动中心', link: '/drivers/intro.html', icon: 'material-symbols:settings-input-component' },
    { text: '任务系统', link: '/jobs/intro.html', icon: 'material-symbols:task-alt-outline' },
    { text: 'Web API', link: '/webapi/intro.html', icon: 'material-symbols:api-outline' },
  ],
  collections: [
    defineCollection({
      type: 'doc',
      dir: '1. introduce',
      title: '介绍',
      linkPrefix: '/introduce/',
      sidebar: 'auto',
    }),
    defineCollection({
      type: 'doc',
      dir: '2. core',
      title: '核心模块',
      linkPrefix: '/core/',
      sidebar: 'auto',
    }),
    defineCollection({
      type: 'doc',
      dir: '3. drivers',
      title: '驱动中心',
      linkPrefix: '/drivers/',
      sidebar: 'auto',
    }),
    defineCollection({
      type: 'doc',
      dir: '4. jobs',
      title: '任务系统',
      linkPrefix: '/jobs/',
      sidebar: 'auto',
    }),
    defineCollection({
      type: 'doc',
      dir: '5. webapi',
      title: 'Web API',
      linkPrefix: '/webapi/',
      sidebar: 'auto',
    }),
  ]
})
EOF

cat << 'EOF' > docs/README.md
---
pageLayout: home
config:
  - type: hero
    full: true
    background: 'tint-plate'
    hero:
      name: UniCon
      tagline: 高性能 .NET 10 工业通讯网关框架
      text: 基于 Clean Architecture 的生产级、可扩展网关核心，原生支持多协议高频采集、任务调度与边缘计算。
      actions:
        - text: 快速开始
          link: /introduce/intro.html
          type: primary
          icon: material-symbols:rocket-launch-outline
        - text: GitHub 仓库
          link: git@github.com:Entity-Now/UniCon.git
          type: secondary
          icon: mdi:github
  - type: features
    features:
      - title: 统一驱动中心
        details: 标准化的 IUniconDriver 接口设计，开箱即用的 Modbus, OPC UA, S7, MQTT 等高性能工业驱动。
        icon: material-symbols:settings-input-component
      - title: 高频并发采集
        details: 摒弃同步锁与 LINQ 轮询，采用 Channel 异步调度及多线程并发通道优化，资源消耗更低。
        icon: material-symbols:speed-outline
      - title: 全异步整洁架构
        details: 遵循 Domain -> Application -> Infrastructure -> Presentation 的依赖注入规范，支持生产级可维护性。
        icon: material-symbols:architecture-outline
      - title: 强稳定性保障
        details: 原生适配自动重连机制，通过异常自愈、优雅停机与完备日志监测保障系统 7*24 小时长效运行。
        icon: material-symbols:security
---
EOF

# Commit files in temp
git add .
git commit -m "chore: init vuepress docs site"

# Push to original local repository to create the docs branch
git push "$REPO_DIR" docs:docs

# Cleanup temp
cd "$REPO_DIR"
rm -rf "$TEMP_DIR"

echo "Done! The 'docs' branch is initialized in the local repository."
