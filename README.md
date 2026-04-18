<div align="center">

# OpenFaka

**基于 .NET 10 的自动化数字商品（卡密）发卡平台**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4?logo=dotnet)
![EF Core](https://img.shields.io/badge/EF%20Core-10-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-336791?logo=postgresql&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-10-512BD4?logo=blazor)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

[English](README.en.md)

</div>

---

## 项目简介

OpenFaka 是一款基于 **.NET 10** 构建的现代化自动发卡平台，专为数字商品（卡密、激活码、会员账号、软件授权等）的自动化交付而设计。平台采用前后端分离架构，提供简洁高效的前台购买体验和功能完备的管理后台，帮助个人站长与小型商户快速搭建属于自己的数字商品销售站点。

> 项目灵感来源于 [Orion Key](https://github.com/RivenLau/orion-key)，在借鉴其优秀功能设计的基础上，全面迁移至 .NET 生态，充分利用 .NET 10 的高性能、跨平台与现代化语言特性。

---

## 截图预览

| 前台首页 | 管理后台 |
|:---:|:---:|
| ![前台首页](docs/assets/home.png) | ![管理后台](docs/assets/admin.png) |

| 商品列表 | 订单管理 |
|:---:|:---:|
| ![商品列表](docs/assets/products.png) | ![订单管理](docs/assets/orders.png) |

---

## 在线 Demo

> 演示环境即将上线，敬请期待。

| | 地址 |
|---|---|
| 🛒 **前台** | `https://demo.openfaka.com` |
| 🛠️ **管理后台** | `https://demo.openfaka.com/admin` |
| 🔑 **管理员账号** | `admin` / `admin123` |

---

## 核心特性

|  |  |
|---|---|
| 🛒 **自动发卡** — 支付成功后即时自动发放卡密，零人工干预，7×24 小时营业 | 🎨 **主题切换** — 支持亮色 / 暗色模式，多主题色自由配置，适配不同品牌风格 |
| 📦 **商品管理** — 支持多级分类、上下架控制、库存预警、卡密批量导入与导出 | 🔒 **安全认证** — JWT Bearer Token 无状态认证 + BCrypt 密码加密，安全可靠 |
| 💳 **多支付渠道** — 可插拔的支付架构，支持易支付、微信支付、支付宝、USDT 等 | 🛡️ **风控系统** — IP 限流、登录防爆破、订单防刷单、异常行为自动拦截 |
| 📊 **数据仪表盘** — 实时销售数据概览、订单趋势图、收入统计、商品热度排行 | 🔍 **订单追踪** — 支持订单号查询卡密，游客购买也能凭订单号自助取货 |
| 🛍️ **购物车系统** — 多商品合并下单、批量结算，提升用户购买体验与客单价 | ⚙️ **站点配置** — 公告栏、弹窗公告、维护模式、SEO 设置，后台一键开关 |
| 👤 **会员中心** — 用户注册登录、订单历史、卡密查询、账户安全管理 | 🔔 **通知系统** — 邮件 / Webhook 订单通知，支持自定义通知模板 |
| 🌍 **国际化支持** — 基于 .NET 本地化框架，支持多语言切换 | 🐳 **容器化部署** — 提供官方 Docker 镜像与 Docker Compose 编排，一键上线 |

---

## 支付渠道集成

| 渠道 | 接入方式 | 状态 |
|------|---------|------|
| 易支付（聚合支付） | 第三方易支付平台 API 接入 | ✅ 已支持 |
| 微信支付 | 易支付集成 / 原生扫码支付 | ✅ 已支持 / 🚧 原生接入开发中 |
| 支付宝 | 易支付集成 / 原生扫码支付 | ✅ 已支持 / 🚧 原生接入开发中 |
| USDT (TRC-20) | BEpusdt 自托管，链上自动确认 | ✅ 已支持 |
| USDT (BEP-20) | BEpusdt 自托管，链上自动确认 | ✅ 已支持 |

> 支付架构采用策略模式设计，可通过后台「支付渠道管理」自由配置、启用/禁用、排序和扩展新渠道。

---

## 技术架构

### 后端技术栈

| 技术 | 版本 | 说明 |
|------|------|------|
| .NET | 10 | 运行时与基础类库 |
| ASP.NET Core | 10 | Web API 框架 |
| Entity Framework Core | 10 | ORM 数据访问 |
| ASP.NET Core Identity | 10 | 用户认证与授权基础 |
| JWT Bearer Authentication | — | 无状态 Token 认证 |
| BCrypt.Net | — | 密码哈希加密 |
| FluentValidation | — | 请求参数校验 |
| AutoMapper | — | 对象映射 |
| Serilog | — | 结构化日志记录 |
| Swagger / OpenAPI | — | API 文档自动生成 |

### 前端技术栈

| 技术 | 版本 | 说明 |
|------|------|------|
| Blazor WebAssembly | 10 | 交互式 SPA 前端框架 |
| MudBlazor | — | Material Design 风格 UI 组件库 |
| Tailwind CSS | 3 | 原子化 CSS 框架 |
| Chart.js / MudBlazor.Charts | — | 数据可视化图表 |

### 基础设施

| 技术 | 版本 | 说明 |
|------|------|------|
| PostgreSQL | 15+ | 主数据库 |
| Redis | 7+ | 缓存、会话、限流计数 |
| Docker | — | 容器化打包与部署 |
| Nginx | — | 反向代理与静态文件服务 |

---

## 项目结构

```
openfaka/
├── src/
│   ├── OpenFaka.Web/                    # ASP.NET Core Web API 后端
│   │   ├── Controllers/                   # API 控制器
│   │   ├── Middlewares/                   # 自定义中间件（异常处理、限流、日志）
│   │   ├── appsettings.json               # 应用配置文件
│   │   └── Program.cs                     # 应用入口
│   ├── OpenFaka.Core/                    # 领域层 / 核心业务逻辑
│   │   ├── Entities/                      # 领域实体
│   │   ├── Interfaces/                    # 仓储与服务接口
│   │   ├── Services/                      # 领域服务
│   │   └── DTOs/                          # 数据传输对象
│   ├── OpenFaka.Infrastructure/            # 基础设施层
│   │   ├── Data/                          # EF Core DbContext、Migrations
│   │   ├── Repositories/                  # 仓储实现
│   │   ├── Services/                      # 外部服务实现（支付、邮件、通知）
│   │   └── Identity/                      # 认证授权实现
│   └── OpenFaka.Web.Client/              # Blazor WebAssembly 前端
│       ├── Pages/                         # 页面组件
│       ├── Shared/                        # 共享布局与组件
│       ├── Services/                      # 前端 HTTP 服务
│       └── wwwroot/                       # 静态资源
├── tests/                                 # 单元测试与集成测试
├── docs/                                  # 文档与截图
├── docker/                                # Docker 相关配置
├── docker-compose.yml                     # 一键编排部署
└── OpenFaka.sln                          # 解决方案文件
```

> 采用 **Clean Architecture + CQRS 风格** 的分层架构，核心领域层独立于框架，便于测试与演进。

---

## 快速开始

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Redis 7+](https://redis.io/download)（可选，用于缓存与限流）
- [Node.js 20+](https://nodejs.org/)（仅前端构建需要）

### 1. 克隆仓库

```bash
git clone https://github.com/xuegaoge/openfaka.git
cd openfaka
```

### 2. 配置数据库

修改 `src/OpenFaka.Web/appsettings.Development.json`：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=openfaka;Username=postgres;Password=your_password"
  },
  "Redis": {
    "ConnectionString": "localhost:6379"
  }
}
```

### 3. 执行数据库迁移

```bash
cd src/OpenFaka.Infrastructure
dotnet ef database update --startup-project ../OpenFaka.Web
```

### 4. 启动后端 API

```bash
cd src/OpenFaka.Web
dotnet run
```

API 默认运行在 `https://localhost:5001`，Swagger 文档地址：`https://localhost:5001/swagger`

### 5. 启动前端（开发模式）

```bash
cd src/OpenFaka.Web.Client
dotnet run
```

前端默认运行在 `https://localhost:5002`

---

## Docker 部署

### 一键启动（推荐）

```bash
docker-compose up -d
```

这将同时启动：
- `openfaka-web` — 后端 API + Blazor 前端
- `openfaka-db` — PostgreSQL 数据库
- `openfaka-redis` — Redis 缓存

访问地址：
- 前台：`http://localhost:8080`
- 管理后台：`http://localhost:8080/admin`
- Swagger API 文档：`http://localhost:8080/swagger`

### 生产环境部署建议

1. 使用 `docker-compose.prod.yml` 配合外部 Nginx 反向代理
2. 配置 SSL 证书（Let's Encrypt / 云服务商证书）
3. 使用云数据库（RDS）与云 Redis，提升可用性
4. 配置日志收集（ELK / Loki）与监控告警（Prometheus + Grafana）

---

## 后台管理

首次运行后，使用默认管理员账号登录后台：

- 地址：`/admin`
- 账号：`admin`
- 密码：`admin123`

> ⚠️ **请务必在首次登录后立即修改默认密码！**

后台功能模块：
- 📊 **仪表盘** — 今日订单、收入、用户增长、商品销量排行
- 📦 **商品管理** — 分类、商品、卡密库存、批量导入导出
- 🛒 **订单管理** — 订单列表、支付状态、手动补单、退款处理
- 👤 **用户管理** — 注册用户、游客订单、黑名单、操作日志
- 💳 **支付渠道** — 渠道配置、费率设置、启用/禁用、排序
- ⚙️ **系统设置** — 站点信息、SEO、公告、主题、邮件、维护模式
- 🛡️ **风控日志** — 限流记录、登录失败、异常订单、IP 封禁

---

## 卡密导入格式

后台支持通过 Excel / CSV 批量导入卡密，文件格式示例：

```csv
card_key,remark
ABC123-DEF456-GHI789,测试卡密-1
XYZ987-WVU654-TSQ321,测试卡密-2
```

导入时自动去重、校验格式，并实时更新商品库存数量。

---

## 路线图

- [x] 项目基础架构搭建（.NET 10 + EF Core + PostgreSQL）
- [x] 用户认证与授权（JWT + Identity）
- [x] 商品与卡密核心模块
- [x] 易支付渠道接入
- [x] 前台购买与自动发卡流程
- [x] 管理后台仪表盘
- [ ] 原生微信支付 / 支付宝扫码支付
- [ ] 会员等级与折扣体系
- [ ] 优惠券与满减活动
- [ ] 多商户（SaaS）模式支持
- [ ] 微信小程序 / H5 适配
- [ ] 插件市场与第三方扩展

---

## 贡献指南

我们欢迎社区贡献！如果您想参与项目开发，请参考以下流程：

1. Fork 本仓库
2. 创建功能分支：`git checkout -b feature/your-feature`
3. 提交代码：`git commit -m "feat: add some feature"`
4. 推送分支：`git push origin feature/your-feature`
5. 发起 Pull Request

请确保代码通过现有测试，并遵循项目的代码风格规范。

---

## 开源协议

本项目基于 [MIT License](LICENSE) 开源，可自由用于个人学习、商业项目或二次开发。

---

## 致谢

- 功能设计参考：[Orion Key](https://github.com/RivenLau/orion-key) — 优秀的 Java + Next.js 发卡平台
- UI 组件：[MudBlazor](https://mudblazor.com/)
- 后端框架：[ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)

---

<div align="center">

**Made with ❤️ by [xuegaoge](https://github.com/xuegaoge) and contributors.**

如果这个项目对您有帮助，请给个 ⭐ Star 支持一下！

</div>
