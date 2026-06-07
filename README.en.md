<div align="center">

# OpenFaka

**Automated Digital Goods (Card Key) Delivery Platform**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4?logo=dotnet&logoColor=white)
![Next.js](https://img.shields.io/badge/Next.js-16-000000?logo=next.js&logoColor=white)
![FreeSql](https://img.shields.io/badge/FreeSql-ORM-512BD4)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

[简体中文](README.md)

</div>

---

## Introduction

OpenFaka is a modern automated digital goods delivery platform for card keys, activation codes, membership accounts, software licenses and more. Payments are processed and card keys delivered automatically — zero manual intervention, 24/7.

Built with frontend-backend separation:
- Backend: ASP.NET Core 10 + FreeSql ORM
- Frontend: Next.js 16 + Tailwind CSS + shadcn/ui
- Database: SQLite (development); for production any FreeSql-supported DB (MySQL or PostgreSQL recommended)

---

## Core Features

### Storefront

| Feature | Description |
|---------|-------------|
| Product Catalog | Category nav, search, product details with specs |
| Shopping Cart | Multi-item, quantity adjust, batch checkout, guest cart |
| Payment Channels | Alipay (Epay), WeChat Pay (Epay), USDT (TRC-20) |
| Auto Delivery | Instant card key assignment after payment |
| Order Query | Look up by order# or email, copy or export TXT |
| Member System | Register, login, order history, password mgmt |

### Admin Dashboard

| Feature | Description |
|---------|-------------|
| Dashboard | Today/month sales, trend chart, low stock alerts |
| Products | CRUD, specs, wholesale pricing, enable/disable |
| Categories | Multi-level category management |
| Card Keys | Batch import (auto-dedup), stock summary, void, batches |
| Orders | Filter, detail view, manually mark paid |
| Payment Channels | AES config, enable/disable |
| Users | List, search, enable/disable accounts |
| Site Config | Site name, announcements, popup, points, maintenance |
| Risk Control | IP limit, fingerprint, purchase limits, Turnstile |
| TXID Review | USDT on-chain confirmation and review |
| Operation Logs | Admin audit trail |

### Technical Highlights

- Dual Token JWT (Access + Refresh) with auto-refresh
- Optimistic + row-level locks for concurrent card key safety
- Webhook idempotency to prevent duplicate delivery
- Device fingerprint (Canvas/WebGL/Audio SHA-256) anti-fraud
- Order idempotency (IdempotencyKey unique index)
- Card key masking (only first/last 4 visible)
- Chinese / English instant switch
- Light/Dark theme + 6 color schemes

---

## Tech Stack

| Technology | Description |
|------------|-------------|
| ASP.NET Core 10 | Web API framework |
| FreeSql 3.x | ORM (supports MySQL, PostgreSQL, SQLite, SQL Server, etc.) |
| Next.js 16 | Frontend framework |
| React 19 | UI library |
| Tailwind CSS 3 + shadcn/ui | Styling and components |
| JWT | Dual-token auth (Access + Refresh) |

---

## Quick Start

```bash
git clone https://github.com/xuegaoge/openfaka.git
cd openfaka

# Backend
cd src/OpenFaka.Web
dotnet run

# Frontend (new terminal)
cd apps/web
npm install
npm run dev
```

| Role | URL | Account |
|------|-----|---------|
| Storefront | http://localhost:3000 | No login needed |
| Admin | http://localhost:3000/admin | admin / admin123 |

> Change the default password after first login!

---

## License

[MIT License](LICENSE)

---

<div align="center">

Made with love by xuegaoge

Star us on GitHub if this project helps you!

</div>
