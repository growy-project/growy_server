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
```

There is no test project in this directory — the parent `CLAUDE.md` mentions `dotnet test`, but it does not apply here.

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

### Calculators

`Calculators/` holds pure stateless math used by the statistics pipeline: `RsiCalculator`, `EmaCalculator`, `CpviCalculator`. Treat these as the math layer — they should not depend on EF, HTTP, or DI. The `Calculators` folder pairs with results in `Models/` (`RsiResult`, `CPVIResult`, etc.) — keep computation and DTOs in their respective folders.

### DbContext Entities

`GrowyDbContext` exposes: `SymbolDatePrices`, `SymbolDatePriceCedears`, `Users`, `Companies`, `UserWatchlist`.

### Auth

JWT Bearer is wired up in `Program.cs` (`AddAuthentication` + `AddJwtBearer`, with `UseAuthentication()`/`UseAuthorization()` in the pipeline). The flow:

1. Frontend posts a Google ID token to `POST /auth/google-login`.
2. `UserService.GoogleLoginAsync` validates the Google `IdToken` against `Google:ClientId`, upserts a `UserEntity` (default `Role = "default"`), and issues a JWT signed with `Jwt:Secret` containing a `"role"` claim (`RoleClaimType = "role"`).
3. Subsequent requests send `Authorization: Bearer <jwt>`.

Endpoint guards use standard attributes: `[Authorize]` for any authenticated user (e.g. `UserController`), `[Authorize(Roles = "admin")]` for admin-only ops (`PUT /symbol/{symbol}/top-growth`, `PUT /symbol/{symbol}/toxic`). Admin gating is now **server-side via the `role` JWT claim** — the role must be set on the `UserEntity` in the DB. The parent CLAUDE.md note about client-side email gating describes the old behavior.

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
- **`Models/`** is the source of truth for data contracts shared with the frontend.
- Raw SQL is used in statistics queries (via `db.Database.SqlQueryRaw`) — use parameterized queries, never string-interpolate user input.
- **Unix timestamps in the DB are in milliseconds.** The API and frontend work in seconds. Always multiply by 1000 before passing a unix date to any SQL query (e.g. `startJobParameters.StartUnixDate * 1000`). Failing to do this results in empty query results with no error.

## Design Principles

- **SOLID & Architecture**
- Single Responsibility Principle (SRP): Every class or service must have one, and only one, reason to change. Separate business logic from infrastructure (e.g., API clients vs. domain models).

- Open/Closed Principle (OCP): Systems should be open for extension but closed for modification. Use the Strategy Pattern for integrations; adding a new exchange should involve creating a new IExchangeStrategy implementation, not modifying existing logic.

- Dependency Inversion (DIP): Always depend on abstractions, not implementations. Inject dependencies via interfaces (e.g., IDatabaseService, IExchangeStrategy) to ensure the code is decoupled and testable.

## Clean Code & Readability

- **Meaningful Names**: Variable, function, and class names should clearly reveal their intent. Avoid abbreviations and ambiguous names.
- **Small Functions**: Functions should be small and do only one thing. Ideally they should be short and focused on a single responsibility.
- **Single Responsibility Principle (SRP)**: A class should have only one reason to change. Each class should handle a single responsibility.
- **Avoid Deep Nesting**: Avoid deeply nested control structures such as multiple if statements. Prefer guard clauses and early returns to keep the code easier to read.
- **Don't Repeat Yourself (DRY)**: Avoid code duplication. If the same logic appears more than once, extract it into a reusable function or component.
- **Comments Are a Last Resort**: Code should be clear enough to explain itself. Comments should explain why something is done rather than what the code is doing.
- **Use Proper Error Handling**: Prefer exceptions over error codes to handle failures and unexpected situations.
- **Keep Function Arguments Minimal**: Functions should have as few parameters as possible, ideally between zero and two.
- **Separate Levels of Abstraction**: Do not mix high-level business logic with low-level implementation details within the same method.
- **Classes Should Be Small**: Classes should be small, focused, and highly cohesive, containing only the data and behavior necessary for their responsibility.
- **Prefer Composition Over Inheritance**: Favor composition when building objects instead of relying on deep inheritance hierarchies.
- **Boy Scout Rule**: Always leave the code better than you found it.

- **Encapsulate Data**: Avoid exposing internal state directly. Use methods or properties to control access.

## Async/Await Best Practices

- **Async All the Way**: Avoid mixing synchronous and asynchronous code. Do not use .Result or .Wait(), as these can lead to deadlocks. Use await consistently from the entry point down.

- **ConfigureAwaiting**: Use .ConfigureAwait(false) in library or infrastructure code where the synchronization context is not required, improving performance and avoiding overhead.

- **Cancellation Tokens**: Always propagate CancellationToken through asynchronous method chains to allow for graceful shutdowns and request cancellations.

- **Avoid Async Void**: Use Task or ValueTask instead of void for asynchronous methods to ensure exceptions are properly caught and handled. Only use async void for event handlers if strictly necessary.

- **Task Overheads**: Use ValueTask for high-frequency methods that often return synchronously to reduce heap allocations.

## Data Structures & Performance

- **(Big-O)Collection Selection**: Choose the right collection based on the operation's time complexity.Use HashSet<T> or Dictionary<TKey, TValue> for $O(1)$ lookups.Avoid calling .Contains() or .FirstOrDefault() on a large List<T> inside a loop ($O(n^2)$ complexity).

## Defensive Programming

- **Validate Inputs**: Never assume inputs are valid, especially from external systems or APIs
- **Use Guard Clauses**: Handle invalid states early to simplify the main logic.
- **Immutable Data When Possible**: Immutable objects reduce side effects and concurrency issues.
