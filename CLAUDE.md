# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Growy Server — Backend Subagent

## Stack

- **Runtime**: .NET 10 (ASP.NET Core Web API)
- **ORM**: Entity Framework Core 10 (`Microsoft.EntityFrameworkCore` 10.0.6)
- **Database**: PostgreSQL only (`Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.1)
- **Auth**: JWT Bearer (`Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.6) + Google ID token (`Google.Apis.Auth` 1.73.0)
- **API docs**: Swagger/OpenAPI (available in Development at `/swagger`)

## Common Commands

Run all commands from inside `growy_server/` (the repo root has multiple .NET projects):

```bash
dotnet run                              # Start API → https://localhost:7138 (Swagger at /swagger in Dev)
dotnet build
dotnet ef migrations add <Name>         # Requires dotnet-ef tool installed globally
dotnet ef database update
dotnet test                             # Run all tests (project at tests/growy_server.Tests/)
dotnet test --filter "FullyQualifiedName~MyTest"   # Run a single test
```

## Architecture

### Service Lifetimes

- `IStatisticsJobService` → **Singleton** (`StatisticsJobService`): holds an in-memory `MemoryCache` of running/completed jobs. Uses `IServiceScopeFactory` to resolve scoped services inside background tasks.
- `IStatisticsService`, `ISymbolService`, `IUserService`, `IWatchlistService` → **Scoped**
- `IEmailService` → **Transient**

### Background Job System (`StatisticsJobService`)

The singleton job service runs two job kinds, both fire-and-forget `Task.Run` with progress reporting:
- `StartJob(StartStatisticJobParameters)` — kicked off by `POST /statistics/start`, computes top-growth stocks.
- `StartWatchlistJob(StartWatchlistJobParameters)` — kicked off by `GET /my-list`, evaluates a user's watchlist over a date range.

Both share the same status surface: jobs are tracked in `MemoryCache` with a 5-minute sliding expiration and polled via `GET /statistics/status/{jobId}` for `PercentComplete`, `Status`, and `Result`. Jobs auto-clear 5 minutes after the last status check.

### Controllers & Routes

| Controller             | Route prefix  | Key endpoints                                                                                                     | Auth                       |
| ---------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------- | -------------------------- |
| `StatisticsController` | `/statistics` | `POST /start`, `GET /history/{symbol}?exchange=`, `GET /status/{jobId}`                                           | anonymous                  |
| `SymbolController`     | `/symbol`     | `GET /date-range?exchange=`, `PUT /{symbol}/top-growth?value=`, `PUT /{symbol}/toxic?value=`, `POST /request-tag` | mixed (admin on tag PUTs)  |
| `UserController`       | `/my-list`    | `POST /symbol`, `DELETE /symbol?symbol=&exchange=`, `GET ?startUnixDate=&endUnixDate=` (kicks watchlist job)      | `[Authorize]` (any user)   |
| `AuthController`       | `/auth`       | `POST /google-login`                                                                                              | anonymous                  |

### Exchange Types

The `exchange` query parameter distinguishes stock types. `"CEDEAR"` hits `symbol_date_price_cedears` table; all other values hit `symbol_date_price` and filter by exchange column.

### Statistics Pipeline (`StatisticsService`)

`GetTopGrowth` and `GetWatchlistGroupAsync` share the same three-query pattern:

1. **Main CTE query** — a single PostgreSQL query computes growth, smoothness (`percent_positive_days`), `return_std_dev`, `max_drawdown`, and `IsInMomentum` (NTILE-based for top-growth; absolute threshold for watchlist) for all symbols in one pass. Returns one row per symbol.
2. **CPVI + RSI + Bounce** — three parallel price-array loads on separate connections, each feeding a pure in-memory calculator: `CpviCalculator`, `RsiCalculator`, `BounceCalculator`. Run concurrently via `Task.WhenAll`.

Results are merged back onto `List<SymbolResult>` by symbol key after `Task.WhenAll`.

### Calculators

`Calculators/` holds pure stateless math used by the statistics pipeline: `RsiCalculator`, `EmaCalculator`, `CpviCalculator`, `BounceCalculator`. These must not depend on EF, HTTP, or DI. Each pairs with a result DTO in `Models/` (`RsiResult`, `CPVIResult`, `BounceResult`, etc.).

- `CpviCalculator` / `BounceCalculator` — both date+exchange-filtered; follow the same `CalculateAsync` + `Compute*` (pure, sync) structure. `BounceCalculator` also takes a `targetPrices` map to apply the analyst-upside gate.
- `RsiCalculator` — loads **full unfiltered history** (no date window), unlike the others.
- `EmaCalculator.Calculate20Ema` — used by `GetSymbolHistory` for the per-symbol detail view; not used by the batch statistics path.

New calculators should mirror the `CpviCalculator` pattern: one `CalculateAsync` for the DB load and one `Compute*` pure method that unit tests target.

### DbContext Entities

`GrowyDbContext` exposes: `SymbolDatePrices`, `SymbolDatePriceCedears`, `Users`, `Companies`, `UserWatchlist`.

### Auth

JWT Bearer is wired up in `Program.cs` (`AddAuthentication` + `AddJwtBearer`, with `UseAuthentication()`/`UseAuthorization()` in the pipeline). The flow:

1. Frontend posts a Google ID token to `POST /auth/google-login`.
2. `UserService.GoogleLoginAsync` validates the Google `IdToken` against `Google:ClientId`, upserts a `UserEntity` (default `Role = "default"`), and issues a JWT signed with `Jwt:Secret` containing a `"role"` claim (`RoleClaimType = "role"`).
3. Subsequent requests send `Authorization: Bearer <jwt>`.

Endpoint guards use standard attributes: `[Authorize]` for any authenticated user (e.g. `UserController`), `[Authorize(Roles = "admin")]` for admin-only ops (`PUT /symbol/{symbol}/top-growth`, `PUT /symbol/{symbol}/toxic`). Admin gating is **server-side via the `role` JWT claim** — the role must be set on the `UserEntity` in the DB. The parent CLAUDE.md note about client-side email gating describes the old behavior.

In `UserController`, the authenticated user id is read from the JWT `sub` / `NameIdentifier` claim via `TryGetUserId`.

### Email

`EmailService` sends tag-request notifications via SMTP. Config lives in the `Smtp` section of `appsettings.json` (Host/Port/Username/Password/From/AdminEmail). Never hardcode credentials — `appsettings.json` is gitignored; use `appsettings.Example.json` as the template.

### Configuration

`appsettings.json` (gitignored) holds: `ConnectionStrings:DefaultConnection`, `Google:ClientId`, `Jwt:{Secret,Issuer,Audience,ExpirationHours}`, and the `Smtp` section. Copy `appsettings.Example.json` and fill in values. The JWT secret should be generated with `openssl rand -base64 64`.

### Watchlist

`IWatchlistService` enforces a per-user symbol cap and surfaces two domain exceptions used by `UserController` to translate to HTTP `409 Conflict`:
- `WatchlistLimitReachedException` — user is at the symbol cap
- `WatchlistDuplicateException` — `(symbol, exchange)` already in the user's list

## Key Conventions

- **New endpoints**: add to an existing controller or create a new one for unrelated domains.
- **New services**: register in `Program.cs` with the appropriate lifetime (most services are `AddScoped`; only use `AddSingleton` for stateful shared services like the job tracker).
- **CORS** (`Program.cs` policy `AllowLocalhost`): allows `http://localhost:3000`, `https://momentum-scanner.com`, and `https://gentle-stone-0ea32490f.7.azurestaticapps.net`. Do not widen without user confirmation. Note: the parent CLAUDE.md says "localhost only" — this server now also serves the deployed frontends.
- **`Models/`** is the source of truth for data contracts shared with the frontend. JSON serialization is default PascalCase (no global naming policy, no `[JsonPropertyName]` attributes) — new fields on `SymbolResult` serialize as-is.
- Raw SQL is used in statistics queries (via `db.Database.SqlQueryRaw`) — use parameterized queries, never string-interpolate user input.
- **Unix timestamps in the DB are in milliseconds.** The API and frontend work in seconds. Always multiply by 1000 before passing a unix date to any SQL query (e.g. `startJobParameters.StartUnixDate * 1000`). Failing to do this results in empty query results with no error.
- **Parallel calculator tasks each need their own `NpgsqlConnection`** — Npgsql connections are not thread-safe; open a separate connection per concurrent task.
