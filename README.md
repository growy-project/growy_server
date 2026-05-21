<div align="center">

# Growy Server

### The momentum engine behind [momentum-scanner.com](https://momentum-scanner.com)

A high-performance ASP.NET Core 10 API that crunches years of historical stock prices into ranked momentum signals — across NYSE, Nasdaq, and CEDEARs.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF_Core-10.0-512BD4?logo=dotnet&logoColor=white)](https://learn.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Npgsql_10-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Swagger](https://img.shields.io/badge/OpenAPI-Swagger-85EA2D?logo=swagger&logoColor=black)](https://swagger.io/)
[![JWT](https://img.shields.io/badge/Auth-JWT_+_Google_OAuth-000000?logo=jsonwebtokens&logoColor=white)](https://jwt.io/)

</div>

---

## What it does

Given a date range and an exchange, the server scans every symbol's price history and ranks the ones with the strongest momentum profile — using **RSI**, **CPVI**, and **EMA** calculators tuned for swing-trading horizons. Heavy work is offloaded to background jobs with a polling API, so the frontend stays responsive while thousands of symbols are scored.

| Capability | Endpoint |
|---|---|
| Kick off a top-growth scan | `POST /statistics/start` |
| Poll job progress | `GET /statistics/status/{jobId}` |
| Pull a symbol's full history | `GET /statistics/history/{symbol}?exchange=` |
| Add to personal watchlist | `POST /my-list/symbol` |
| Score the watchlist over a range | `GET /my-list?startUnixDate=&endUnixDate=` |
| Sign in with Google | `POST /auth/google-login` |
| Tag a symbol (admin only) | `PUT /symbol/{symbol}/top-growth?value=` |

Full schema lives in Swagger at `/swagger` when running in Development.

---

## Architecture

```
                            ┌──────────────────────────────┐
                            │   Next.js board (3000)       │
                            │   momentum-scanner.com       │
                            └──────────────┬───────────────┘
                                           │  HTTPS + JWT
                                           ▼
        ┌──────────────────────────────────────────────────────────┐
        │                  growy_server  (7138)                    │
        │                                                          │
        │  Controllers ─► Services ─► Calculators (RSI/CPVI/EMA)   │
        │                  │                                       │
        │                  ▼                                       │
        │       ┌──────────────────────┐                           │
        │       │ StatisticsJobService │  Singleton, MemoryCache   │
        │       │ (fire-and-forget)    │  5-min sliding expiry     │
        │       └──────────┬───────────┘                           │
        │                  │                                       │
        │                  ▼                                       │
        │              EF Core 10                                  │
        └─────────────────┬────────────────────────────────────────┘
                          │
                          ▼
                    ┌─────────────┐
                    │ PostgreSQL  │
                    │  growy_db   │
                    └─────────────┘
```

### Key pieces

- **`StatisticsJobService`** — singleton job tracker. `StartJob` and `StartWatchlistJob` spawn fire-and-forget `Task.Run` workers; each job's progress is exposed through a shared status surface (`PercentComplete`, `Status`, `Result`) backed by `IMemoryCache`.
- **`Calculators/`** — pure, stateless math (`RsiCalculator`, `EmaCalculator`, `CpviCalculator`). No EF, no HTTP — just numbers in, results out.
- **`StatisticsService` / `SymbolService` / `WatchlistService`** — scoped EF-aware services that bridge calculators and raw SQL queries against `symbol_date_price` / `symbol_date_price_cedears`.
- **`UserService` + JWT** — Google ID token comes in, signed JWT (8h, `role` claim) goes out. Admins are gated server-side via `[Authorize(Roles = "admin")]`.

---

## Quick start

```bash
# 1. Configure
cp appsettings.Example.json appsettings.json
# Fill in: ConnectionStrings:DefaultConnection, Google:ClientId,
#         Jwt:Secret (openssl rand -base64 64), Smtp:*

# 2. Database
dotnet ef database update

# 3. Run
dotnet run
# → https://localhost:7138
# → Swagger UI at https://localhost:7138/swagger
```

### Configuration keys

| Section | Key | Purpose |
|---|---|---|
| `ConnectionStrings` | `DefaultConnection` | Postgres connection string |
| `Google` | `ClientId` | Google OAuth client id (audience for ID tokens) |
| `Jwt` | `Secret` / `Issuer` / `Audience` / `ExpirationHours` | JWT signing + validation |
| `Smtp` | `Host` / `Port` / `Username` / `Password` / `From` / `AdminEmail` | Tag-request notifications |

`appsettings.json` is gitignored — never commit secrets.

---

## Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 (ASP.NET Core Web API) |
| ORM | Entity Framework Core 10.0.6 |
| Database | PostgreSQL (Npgsql.EntityFrameworkCore.PostgreSQL 10.0.1) |
| Auth | JWT Bearer 10.0.6 + Google.Apis.Auth 1.73.0 |
| API docs | Swashbuckle 10.0.0 (Swagger / OpenAPI) |
| Hosting | Azure (deployed alongside an Azure Static Web App frontend) |

---

## Routes at a glance

| Controller | Prefix | Auth |
|---|---|---|
| `StatisticsController` | `/statistics` | anonymous |
| `SymbolController` | `/symbol` | mixed — `[Authorize(Roles = "admin")]` on tag PUTs |
| `UserController` | `/my-list` | `[Authorize]` |
| `AuthController` | `/auth` | anonymous |

---

## Project layout

```
growy_server/
├── Controllers/   # Thin HTTP layer
├── Services/      # Business logic, interfaces + impls
├── Calculators/   # Pure momentum math (RSI, EMA, CPVI)
├── Models/        # DTOs + DB entities (Models/DB)
├── Data/          # GrowyDbContext
├── docs/          # Strategy notes & domain papers
└── Program.cs     # DI, CORS, JWT bearer wire-up
```

---

## Conventions worth knowing

- **Unix timestamps in the DB are milliseconds.** API and frontend speak seconds. Multiply by 1000 before passing to any raw SQL.
- **Raw SQL** is used in the hot statistics path via `db.Database.SqlQueryRaw` — always parameterize, never interpolate.
- **CORS** allows `localhost:3000`, `momentum-scanner.com`, and the Azure Static Apps URL. Widening requires explicit confirmation.
- **New services** go in `Program.cs` — default to `AddScoped`; reserve `AddSingleton` for stateful shared state like the job tracker.

For deeper architectural and style guidance, see [`CLAUDE.md`](./CLAUDE.md).
