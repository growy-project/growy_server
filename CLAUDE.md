# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# Growy Server — Backend Subagent

## Stack

- **Runtime**: .NET 10 (ASP.NET Core Web API)
- **ORM**: Entity Framework Core 10
- **Database**: PostgreSQL only (Npgsql / EF Core 9)
- **API docs**: Swagger/OpenAPI (available in Development at `/swagger`)

## Common Commands

```bash
dotnet run                              # Start API
dotnet build                            # Build
dotnet ef migrations add <Name>         # Add EF migration
dotnet ef database update               # Apply migrations
```

## Architecture

### Service Lifetimes

- `IStatisticsJobService` → **Singleton** (`StatisticsJobService`): holds an in-memory `MemoryCache` of running/completed jobs. Uses `IServiceScopeFactory` to resolve scoped `IStatisticsService` inside background tasks.
- `IStatisticsService`, `ISymbolService`, `IUserService` → **Scoped**
- `IEmailService` → **Transient**

### Background Job System (`StatisticsJobService`)

`POST /statistics/start` kicks off a fire-and-forget `Task.Run` that computes top-growth stocks. The job is tracked in `MemoryCache` with a 5-minute sliding expiration. Poll `GET /statistics/status/{jobId}` for progress (`PercentComplete`, `Status`, `Result`). Jobs auto-clear 5 minutes after the last status check.

### Controllers & Routes

| Controller             | Route prefix  | Key endpoints                                                                                                     |
| ---------------------- | ------------- | ----------------------------------------------------------------------------------------------------------------- |
| `StatisticsController` | `/statistics` | `POST /start`, `GET /history/{symbol}?exchange=`, `GET /status/{jobId}`                                           |
| `SymbolController`     | `/symbol`     | `GET /date-range?exchange=`, `PUT /{symbol}/top-growth?value=`, `PUT /{symbol}/toxic?value=`, `POST /request-tag` |
| `AuthController`       | `/auth`       | `POST /google-login`                                                                                              |

### Exchange Types

The `exchange` query parameter distinguishes stock types. `"CEDEAR"` hits `symbol_date_price_cedears` table; all other values hit `symbol_date_price` and filter by exchange column.

### DbContext Entities

`GrowyDbContext` exposes: `SymbolDatePrices`, `SymbolDatePriceCedears`, `Users`, `Companies`.

### Auth

Google OAuth is handled by `UserService.GoogleLoginAsync` — validates a Google `IdToken` and returns a user + token pair. No ASP.NET Core auth middleware is wired up yet; auth is manual per-endpoint.

### Email

`EmailService` sends tag-request notifications via SMTP. Config lives in `appsettings.json` (never hardcode credentials).

## Key Conventions

- **New endpoints**: add to an existing controller or create a new one for unrelated domains.
- **New services**: register in `Program.cs` with the appropriate lifetime (most services are `AddScoped`; only use `AddSingleton` for stateful shared services like the job tracker).
- **CORS**: `http://localhost:3000` only — do not widen without user confirmation.
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
