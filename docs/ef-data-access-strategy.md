# EF Core Data Access Strategy in `growy_server`

## Background: The Three EF Loading Strategies

Entity Framework Core offers three strategies for loading related data through navigation properties:

| Strategy | How it works | EF API |
|---|---|---|
| **Eager Loading** | Loads related entities in the same query using a SQL JOIN | `.Include()` |
| **Lazy Loading** | Loads related entities automatically on first property access (extra round-trip per access) | `UseLazyLoadingProxies()` + `virtual` nav props |
| **Explicit Loading** | Loads related entities on demand when you explicitly ask for them | `db.Entry(entity).Collection(...).LoadAsync()` |

All three strategies exist to traverse **navigation properties** — relationships mapped between entities (e.g. a `User` that holds a collection of `Orders`).

---

## How `growy_server` Uses EF Core

### `GrowyDbContext` has no navigation properties

```csharp
public class GrowyDbContext : DbContext
{
    public DbSet<SymbolDatePrice>       SymbolDatePrices       { get; set; }
    public DbSet<SymbolDatePriceCedear> SymbolDatePriceCedears { get; set; }
    public DbSet<UserEntity>            Users                  { get; set; }
    public DbSet<CompanyEntity>         Companies              { get; set; }
}
```

None of the four entities has `virtual` navigation properties, no `.HasMany()` / `.HasOne()` relationships are configured in `OnModelCreating`, and `UseLazyLoadingProxies()` is not called. The entities are flat, denormalized rows with no EF-mapped relationships between them.

**Consequence:** none of the three loading strategies applies — there is nothing to traverse.

---

## Two Data Access Patterns

The project deliberately splits data access into two distinct approaches depending on the operation type.

### Pattern 1 — Raw ADO.NET via Npgsql (heavy analytical queries)

Used in: `StatisticsService`, `CpviCalculator`, `RsiCalculator`

```csharp
await using var connection = new NpgsqlConnection(_connectionString);
await connection.OpenAsync(cancellationToken);

await using var command = new NpgsqlCommand(query, connection);
// ... parameterized SQL with CTEs, window functions, multi-table JOINs
await using var reader = await command.ExecuteReaderAsync(cancellationToken);
```

EF Core is bypassed entirely. Queries are hand-written SQL that use:
- **CTEs** to compute first/last prices per symbol over a date range
- **Window functions** (`ROW_NUMBER`, `LAG`, `FIRST_VALUE`) for price ordering and volatility
- **Multi-table JOINs** (`LEFT JOIN companies`) to enrich results in a single round-trip
- **Aggregations** across thousands of rows for RSI and CPVI calculations

### Pattern 2 — EF LINQ for simple flat-entity CRUD

Used in: `UserService`, `SymbolService`

```csharp
// Load one entity, optionally insert (UserService)
var user = await db.Users.FirstOrDefaultAsync(u => u.Email == payload.Email, ct);
if (user == null) { db.Users.Add(newUser); await db.SaveChangesAsync(ct); }

// Aggregate scalar (SymbolService)
var firstDate = await db.SymbolDatePrices
    .Where(p => p.Exchange == exchange)
    .MinAsync(p => p.UnixDate, ct);

// Read-modify-write a single row (SymbolService)
var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol, ct);
company.IsTopGrowth = value;
await db.SaveChangesAsync(ct);
```

EF's change tracker detects that the entity was modified and issues the correct `UPDATE` automatically on `SaveChangesAsync` — no hand-written `UPDATE` SQL needed.

---

## Why This Split Is the Right Design

### Raw SQL where it matters: performance and expressiveness

The core statistics workflow operates on price series for thousands of symbols simultaneously. SQL window functions and CTEs are the natural tool for this class of problem — they express the intent clearly and execute in a single server-side pass. Mapping these operations through EF's LINQ provider would either be impossible (EF cannot generate arbitrary window functions) or require multiple round-trips that defeat the purpose.

Keeping raw SQL here also means:
- **One round-trip** for the growth computation + company enrichment join, regardless of how many symbols match the threshold.
- **No object materialization overhead** for intermediate rows that are never surfaced to the caller.
- **Full control** over parameterization, which is critical for safety — user-supplied dates, thresholds, and exchange values never reach the SQL string directly.

### EF LINQ where it adds value: change tracking for CRUD

For single-row reads and updates (`UserService`, `SymbolService`), EF's change tracker eliminates boilerplate. Loading a `CompanyEntity`, mutating a flag, and calling `SaveChangesAsync` is cleaner and less error-prone than writing a parameterized `UPDATE` by hand. The overhead of EF's identity map and proxy generation is negligible for one-row operations.

### No navigation properties: intentional denormalization

The price tables (`symbol_date_price`, `symbol_date_price_cedears`) contain millions of rows and are written by external pipeline jobs. Mapping them as EF navigation properties on `CompanyEntity` would tempt accidental N+1 loading and make the join surface ambiguous (which table? which date range?). Keeping the join explicit in SQL — where it is visible, reviewable, and parameterized — is safer and more maintainable at this scale.

### Decision breakdown

- **Statistics, CPVI, RSI** — raw `NpgsqlConnection` / `NpgsqlCommand`
  - Window functions, CTEs, and multi-symbol aggregations across millions of rows have no ergonomic EF equivalent.

- **Cross-entity joins** — inline SQL `LEFT JOIN`
  - Keeps everything in a single round-trip; no EF navigation property needed.

- **User upsert, company flag update** — EF LINQ + `SaveChangesAsync`
  - Change tracking eliminates hand-written `UPDATE` SQL; single-row overhead is negligible.

- **EF loading strategies (Eager / Lazy / Explicit)** — not used
  - No navigation properties are defined on any entity, so there is nothing to traverse.

---

## Summary

The project uses EF Core as a lightweight change tracker and LINQ query surface for simple CRUD, and delegates all relational complexity to raw SQL. This is the correct balance for a read-heavy analytical API backed by large time-series price tables.

- **Statistics, CPVI, RSI** → raw `NpgsqlConnection` / `NpgsqlCommand`
  - Window functions, CTEs, multi-symbol aggregations across millions of rows.
- **Cross-entity joins** → inline SQL `LEFT JOIN`
  - Single round-trip; no EF navigation property needed.
- **User upsert, company flag update** → EF LINQ + `SaveChangesAsync`
  - Change tracking eliminates hand-written `UPDATE`; single-row overhead is negligible.
- **EF loading strategies (Eager / Lazy / Explicit)** → not used
  - No navigation properties are defined — there is nothing to traverse.
