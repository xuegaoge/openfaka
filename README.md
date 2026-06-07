<div align="center">

# OpenFaka

**基于 ASP.NET Core 的数字商品（卡密）自动发卡平台**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4?logo=dotnet&logoColor=white)
![Next.js](https://img.shields.io/badge/Next.js-16-000000?logo=next.js&logoColor=white)
![FreeSql](https://img.shields.io/badge/FreeSql-ORM-512BD4)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

[English](README.en.md)

</div>

---

## 项目简介

OpenFaka 是一款现代化的数字商品自动发卡平台，专为**卡密、激活码、会员账号、软件授权码**等数字商品的自动化交付而设计。支付成功后系统即时自动发货，全程无需人工干预。

平台采用**前后端分离架构**：
- **后端**：ASP.NET Core 10 + FreeSql ORM
- **前端**：Next.js 16 + Tailwind CSS + shadcn/ui
- **数据库**：SQLite（开发），生产环境可选用 FreeSql 支持的任何数据库（推荐 MySQL 或 PostgreSQL）

---

## 核心功能

### 前台

| 功能 | 说明 |
|------|------|
| 🏪 **商品展示** | 分类导航、搜索排序、商品详情与规格选择 |
| 🛒 **购物车** | 多商品添加、数量调整、批量结算，支持访客购物车 |
| 💳 **多支付渠道** | 支付宝（易支付）、微信支付（易支付）、USDT (TRC-20) |
| 📦 **自动发货** | 支付成功后毫秒级自动分配卡密，7×24 无人值守 |
| 🔍 **订单查询** | 凭订单号或邮箱随时查询卡密，支持复制与导出 TXT |
| 👤 **会员系统** | 注册登录、订单历史、密码管理 |

### 管理后台

| 功能 | 说明 |
|------|------|
| 📊 **仪表盘** | 今日/本月销售额、订单量、销售趋势图、低库存预警 |
| 📦 **商品管理** | 商品 CRUD、规格管理、批发阶梯价、上下架控制 |
| 🏷️ **分类管理** | 多级分类管理 |
| 🔑 **卡密管理** | 批量导入（自动去重）、库存汇总、单条/批量作废、批次追溯 |
| 🛒 **订单管理** | 订单筛选（状态/支付方式/类型）、详情查看、手动标记已支付 |
| 💳 **支付渠道** | 渠道配置（AES 加密存储）、启用/禁用 |
| 👤 **用户管理** | 用户列表、搜索、启用/禁用账户 |
| ⚙️ **站点配置** | 站点名称、公告、弹窗、积分、维护模式 |
| 🛡️ **风控管理** | IP 限流、设备指纹限制、购买限制、Cloudflare Turnstile |
| 🔗 **TXID 审核** | USDT 交易链上确认与人工审核 |
| 📋 **操作日志** | 管理员操作审计日志 |

### 技术特性

- **双 Token JWT 认证**（Access + Refresh），自动刷新
- **乐观锁 + 行锁** 保证卡密分配并发安全
- **Webhook 回调幂等性**，防止重复发货
- **设备指纹**（Canvas/WebGL/Audio SHA-256）防刷单
- **订单幂等性**（IdempotencyKey 唯一索引）
- **卡密内容脱敏**（首尾 4 位可见）
- **中英文双语** 即时切换
- **明暗主题 + 6 套配色** 自由切换

---

## 支付渠道

| 渠道 | 接入方式 | 状态 |
|------|---------|------|
| 支付宝 | 易支付聚合 API | ✅ |
| 微信支付 | 易支付聚合 API | ✅ |
| USDT (TRC-20) | BEpusdt 自托管 | ✅ |

> 支付架构基于**策略模式**，通过后台即可配置、扩展新渠道。
---

---

## 技术栈

| 技术 | 说明 |
|------|------|
| ASP.NET Core 10 | Web API 框架 |
| FreeSql | ORM |
| Next.js 16 | 前端框架 |
| shadcn/ui | UI 组件库 |
| JWT | 认证 |

## 快速开始

```bash
git clone https://github.com/xuegaoge/openfaka.git
cd openfaka

# 启动后端
cd src/OpenFaka.Web
dotnet run

# 启动前端（新终端）
cd apps/web
npm install
npm run dev
```

后台地址：http://localhost:3000/admin
账号：admin / admin123

## 开源协议

MIT License
