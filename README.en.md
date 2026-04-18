<div align="center">

# OpenFaka

**Automated Digital Goods (Card Key) Delivery Platform built on .NET 10**

[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-10-512BD4?logo=dotnet)
![EF Core](https://img.shields.io/badge/EF%20Core-10-512BD4?logo=dotnet)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-336791?logo=postgresql&logoColor=white)
![Blazor](https://img.shields.io/badge/Blazor-10-512BD4?logo=blazor)
![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?logo=docker&logoColor=white)

[简体中文](README.md)

</div>

---

## Introduction

OpenFaka is a modern automated digital goods delivery platform built on **.NET 10**, designed for the automated distribution of digital products (card keys, activation codes, membership accounts, software licenses, etc.). The platform adopts a frontend-backend separation architecture, providing a clean and efficient storefront purchase experience and a fully-featured admin dashboard, helping individual webmasters and small merchants quickly set up their own digital goods sales site.

> Inspired by [Orion Key](https://github.com/RivenLau/orion-key), this project migrates the excellent functional design to the .NET ecosystem, fully leveraging the high performance, cross-platform capabilities, and modern language features of .NET 10.

---

## Preview

| Storefront | Admin Dashboard |
|:---:|:---:|
| ![Storefront](docs/assets/home.png) | ![Admin](docs/assets/admin.png) |

| Product List | Order Management |
|:---:|:---:|
| ![Products](docs/assets/products.png) | ![Orders](docs/assets/orders.png) |

---

## Live Demo

> Demo environment coming soon.

| | URL |
|---|---|
| 🛒 **Storefront** | `https://demo.openfaka.com` |
| 🛠️ **Admin** | `https://demo.openfaka.com/admin` |
| 🔑 **Admin Account** | `admin` / `admin123` |

---

## Core Features

|  |  |
|---|---|
| 🛒 **Auto Delivery** — Instant automated card key delivery after successful payment, zero manual intervention, 24/7 operation | 🎨 **Theme Switching** — Light / dark mode support, customizable theme colors to match your brand |
| 📦 **Product Management** — Multi-level categories, shelf control, stock alerts, batch card key import/export | 🔒 **Secure Auth** — JWT Bearer Token stateless authentication + BCrypt password hashing |
| 💳 **Multiple Payment Channels** — Pluggable payment architecture, supports Epay, WeChat Pay, Alipay, USDT, etc. | 🛡️ **Risk Control** — IP rate limiting, login brute-force protection, order fraud prevention, abnormal behavior interception |
| 📊 **Data Dashboard** — Real-time sales overview, order trend charts, revenue statistics, product popularity ranking | 🔍 **Order Tracking** — Query card keys by order number, guest purchases can self-service retrieve keys |
| 🛍️ **Shopping Cart** — Multi-product combined checkout, batch settlement, improving UX and average order value | ⚙️ **Site Configuration** — Announcement bar, popup notices, maintenance mode, SEO settings, one-click toggle in admin |
| 👤 **Member Center** — User registration/login, order history, card key query, account security management | 🔔 **Notification System** — Email / Webhook order notifications, custom notification templates |
| 🌍 **Internationalization** — Based on .NET localization framework, multi-language switching | 🐳 **Containerized Deployment** — Official Docker image and Docker Compose orchestration, one-click deployment |

---

## Payment Channels

| Channel | Integration | Status |
|---------|-------------|--------|
| Epay (Aggregated) | Third-party Epay platform API | ✅ Supported |
| WeChat Pay | Epay integration / Native QR code | ✅ Supported / 🚧 Native dev in progress |
| Alipay | Epay integration / Native QR code | ✅ Supported / 🚧 Native dev in progress |
| USDT (TRC-20) | BEpusdt self-hosted, on-chain auto-confirmation | ✅ Supported |
| USDT (BEP-20) | BEpusdt self-hosted, on-chain auto-confirmation | ✅ Supported |

> Payment architecture uses the Strategy pattern, allowing free configuration, enable/disable, sorting, and extension of new channels via the admin "Payment Channel Management".

---

## Tech Stack

### Backend

| Technology | Version | Description |
|------------|---------|-------------|
| .NET | 10 | Runtime and base class library |
| ASP.NET Core | 10 | Web API framework |
| Entity Framework Core | 10 | ORM data access |
| ASP.NET Core Identity | 10 | User authentication and authorization |
| JWT Bearer Authentication | — | Stateless token authentication |
| BCrypt.Net | — | Password hashing |
| FluentValidation | — | Request parameter validation |
| AutoMapper | — | Object mapping |
| Serilog | — | Structured logging |
| Swagger / OpenAPI | — | Auto-generated API documentation |

### Frontend

| Technology | Version | Description |
|------------|---------|-------------|
| Blazor WebAssembly | 10 | Interactive SPA frontend framework |
| MudBlazor | — | Material Design UI component library |
| Tailwind CSS | 3 | Atomic CSS framework |
| Chart.js / MudBlazor.Charts | — | Data visualization charts |

### Infrastructure

| Technology | Version | Description |
|------------|---------|-------------|
| PostgreSQL | 15+ | Primary database |
| Redis | 7+ | Cache, session, rate limiting |
| Docker | — | Containerized packaging and deployment |
| Nginx | — | Reverse proxy and static file serving |

---

## Project Structure

```
openfaka/
├── src/
│   ├── OpenFaka.Web/                    # ASP.NET Core Web API Backend
│   │   ├── Controllers/                   # API Controllers
│   │   ├── Middlewares/                   # Custom middleware (exception handling, rate limiting, logging)
│   │   ├── appsettings.json               # App configuration
│   │   └── Program.cs                     # Application entry point
│   ├── OpenFaka.Core/                    # Domain layer / Core business logic
│   │   ├── Entities/                      # Domain entities
│   │   ├── Interfaces/                    # Repository and service interfaces
│   │   ├── Services/                      # Domain services
│   │   └── DTOs/                          # Data Transfer Objects
│   ├── OpenFaka.Infrastructure/            # Infrastructure layer
│   │   ├── Data/                          # EF Core DbContext, Migrations
│   │   ├── Repositories/                  # Repository implementations
│   │   ├── Services/                      # External service implementations (payment, email, notifications)
│   │   └── Identity/                      # Authentication and authorization implementation
│   └── OpenFaka.Web.Client/              # Blazor WebAssembly Frontend
│       ├── Pages/                         # Page components
│       ├── Shared/                        # Shared layouts and components
│       ├── Services/                      # Frontend HTTP services
│       └── wwwroot/                       # Static assets
├── tests/                                 # Unit and integration tests
├── docs/                                  # Documentation and screenshots
├── docker/                                # Docker configurations
├── docker-compose.yml                     # One-click orchestration
└── OpenFaka.sln                          # Solution file
```

> Adopts **Clean Architecture + CQRS-style** layered architecture. The core domain layer is framework-independent for easy testing and evolution.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [PostgreSQL 15+](https://www.postgresql.org/download/)
- [Redis 7+](https://redis.io/download) (optional, for cache and rate limiting)
- [Node.js 20+](https://nodejs.org/) (only needed for frontend build)

### 1. Clone the repository

```bash
git clone https://github.com/xuegaoge/openfaka.git
cd openfaka
```

### 2. Configure Database

Edit `src/OpenFaka.Web/appsettings.Development.json`:

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

### 3. Run Database Migrations

```bash
cd src/OpenFaka.Infrastructure
dotnet ef database update --startup-project ../OpenFaka.Web
```

### 4. Start Backend API

```bash
cd src/OpenFaka.Web
dotnet run
```

API runs at `https://localhost:5001` by default. Swagger docs: `https://localhost:5001/swagger`

### 5. Start Frontend (Development Mode)

```bash
cd src/OpenFaka.Web.Client
dotnet run
```

Frontend runs at `https://localhost:5002` by default.

---

## Docker Deployment

### One-click Start (Recommended)

```bash
docker-compose up -d
```

This will start:
- `openfaka-web` — Backend API + Blazor Frontend
- `openfaka-db` — PostgreSQL database
- `openfaka-redis` — Redis cache

Access URLs:
- Storefront: `http://localhost:8080`
- Admin Dashboard: `http://localhost:8080/admin`
- Swagger API Docs: `http://localhost:8080/swagger`

### Production Deployment Recommendations

1. Use `docker-compose.prod.yml` with external Nginx reverse proxy
2. Configure SSL certificates (Let's Encrypt / cloud provider)
3. Use cloud database (RDS) and cloud Redis for higher availability
4. Set up log aggregation (ELK / Loki) and monitoring alerts (Prometheus + Grafana)

---

## Admin Dashboard

After first run, log in to the admin dashboard with the default admin account:

- URL: `/admin`
- Username: `admin`
- Password: `admin123`

> ⚠️ **Please change the default password immediately after first login!**

Admin Modules:
- 📊 **Dashboard** — Today's orders, revenue, user growth, product sales ranking
- 📦 **Product Management** — Categories, products, card key inventory, batch import/export
- 🛒 **Order Management** — Order list, payment status, manual fulfillment, refund handling
- 👤 **User Management** — Registered users, guest orders, blacklist, operation logs
- 💳 **Payment Channels** — Channel config, fee settings, enable/disable, sorting
- ⚙️ **System Settings** — Site info, SEO, announcements, themes, email, maintenance mode
- 🛡️ **Risk Control Logs** — Rate limit records, login failures, abnormal orders, IP bans

---

## Card Key Import Format

The admin supports batch import of card keys via Excel / CSV. File format example:

```csv
card_key,remark
ABC123-DEF456-GHI789,Test Key 1
XYZ987-WVU654-TSQ321,Test Key 2
```

Import automatically deduplicates, validates format, and updates product stock in real-time.

---

## Roadmap

- [x] Project foundation (.NET 10 + EF Core + PostgreSQL)
- [x] User authentication and authorization (JWT + Identity)
- [x] Product and card key core modules
- [x] Epay channel integration
- [x] Storefront purchase and auto delivery flow
- [x] Admin dashboard
- [ ] Native WeChat Pay / Alipay QR code payment
- [ ] Membership tiers and discount system
- [ ] Coupons and discount campaigns
- [ ] Multi-merchant (SaaS) mode
- [ ] WeChat Mini Program / H5 adaptation
- [ ] Plugin marketplace and third-party extensions

---

## Contributing

We welcome community contributions! To participate in project development, please follow this workflow:

1. Fork this repository
2. Create a feature branch: `git checkout -b feature/your-feature`
3. Commit your changes: `git commit -m "feat: add some feature"`
4. Push to the branch: `git push origin feature/your-feature`
5. Open a Pull Request

Please ensure your code passes existing tests and follows the project's code style guidelines.

---

## License

This project is open-sourced under the [MIT License](LICENSE), freely usable for personal learning, commercial projects, or secondary development.

---

## Acknowledgements

- Feature design reference: [Orion Key](https://github.com/RivenLau/orion-key) — Excellent Java + Next.js card key platform
- UI Components: [MudBlazor](https://mudblazor.com/)
- Backend Framework: [ASP.NET Core](https://dotnet.microsoft.com/apps/aspnet)

---

<div align="center">

**Made with ❤️ by [xuegaoge](https://github.com/xuegaoge) and contributors.**

If this project helps you, please give it a ⭐ Star!

</div>
