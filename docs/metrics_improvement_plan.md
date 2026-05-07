# Metrics Improvement Plan — `GetTopGrowth` columns

A scoped engineering backlog for aligning the columns returned by `Services/StatisticsService.cs::GetTopGrowth` with the academic momentum literature consolidated in [`momentum_trading_summary.md`](./momentum_trading_summary.md).

This is an **internal planning doc**. No code changes have been made yet — these are the proposed additions and reframings, ordered by effort.

---

## 1. Context

`GetTopGrowth` is the core endpoint behind `POST /statistics/start`. It scans `symbol_date_price` (or `symbol_date_price_cedears` for the CEDEAR exchange) over a user-supplied date range, computes a growth metric per symbol, joins fundamentals from `companies`, and decorates each row with two derived metrics — `Volatility` (CPVI) and `Rsi`.

The objective of this doc is to answer two questions:
- **Do the existing decorations (`Volatility`, `Rsi`) match the metrics that the literature actually uses?**
- **What additional columns would close the gap between the current implementation and the institutional momentum framework described in `momentum_trading_summary.md`?**

The answer to the first is *partially* — there is conceptual adjacency but not direct correspondence. The answer to the second is *yes, several*, in increasing order of effort.

---

## 2. Current columns vs. the literature

### `PercentageChange` ↔ §1 "Definition: 12-1 momentum window" of the summary

**Match: partial.** The academic-standard momentum signal is the **past 12-month return, excluding the most recent month** (Asness 1994; Jegadeesh & Titman 1993). Today the code accepts an arbitrary user-chosen window and uses raw start-vs-end percentage change:

```sql
((e.end_price - s.start_price) / s.start_price) * 100 AS percentageChange
```

There is no mechanism to skip the most recent month, which the literature attributes to short-term reversal and microstructure noise.

### `Volatility` (CPVI) ↔ §6 "Smooth vs. Choppy" + §7 "Risk-adjusted scaling"

**Match: conceptually adjacent, but not the metric the literature uses.**

CPVI (`Calculators/CpviCalculator.cs`) is `Σ│Δprice│ / │end - start│` — total absolute path length divided by net displacement. A perfectly straight climb gives CPVI = 1; choppier paths give larger values. As a proxy for Wesley Gray's "smooth vs. choppy" idea (§6), it is defensible. But two issues:

1. Gray's actual metric in the summary is **percentage of positive return days**, which is simpler, more interpretable, and is what the cited source uses.
2. BlackRock/AQR-style risk-adjusted scaling (§7) divides momentum by the **standard deviation of returns**, not by a path/displacement ratio. CPVI is *not* a substitute for that.
3. CPVI is currently metadata only — it never affects ranking. The institutional standard explicitly says momentum should be **scaled by volatility before ranking**, which the current ranking (`ORDER BY percentageChange DESC`) does not do.

### `Rsi` ↔ (nothing in the summary)

**Match: none.** RSI (Wilder 1978) is a technical-analysis oscillator from a different tradition than the academic momentum-factor literature. None of the sources cited in the summary (JPM/Asness, Carhart, Gray, AQR, BlackRock) use or recommend RSI as a momentum signal.

Two further notes:
- RSI is fine as a screen for "is this stock currently overbought" — but it should not be presented as a momentum signal.
- The current implementation in `RsiCalculator.cs:26-30` has no date filter; it computes RSI over **all available history** for the symbol, then takes the latest 14 periods. So `Rsi` reflects "current overbought/oversold state," not "RSI within the analysis window."

---

## 3. Gap analysis

| From the summary | Current column | Recommended action |
|---|---|---|
| 12-1 momentum (12-month return, skip most recent month) | only raw window return | **Add** `Momentum_12_1`, or make skip-recent-month logic an option on the existing window |
| Standard deviation of daily returns (§7 risk-adjusted scaling) | none | **Add** `ReturnStdDev` |
| % of positive return days (§6 Gray "smoothness") | CPVI is a proxy, not the canonical metric | **Add** `PercentPositiveDays` |
| Beta vs. benchmark (§6 Gray "boring vs. lottery") | none | **Add** `Beta` (medium effort — needs benchmark series) |
| Max drawdown within window (§5 crash mechanics) | none | **Add** `MaxDrawdown` |
| Risk-adjusted ranking (§7 institutional standard) | ranks by raw `PercentageChange` | **Optional**: introduce `RiskAdjustedScore = PercentageChange / ReturnStdDev` as an opt-in sort |

---

## 4. Recommended additions (ordered by effort)

Each item is independently useful — there is no implicit ordering dependency until step 5 (risk-adjusted ranking, which depends on `ReturnStdDev`).

### 4.1. `PercentPositiveDays`

The smallest, highest signal-quality win. Pure SQL, computed in the same pass as `PercentageChange`.

Approach: extend the existing growth CTE in `StatisticsService.cs:48-57` to also count days with positive `close_price - LAG(close_price)` and divide by total day count for the symbol within the window. No new calculator needed.

Maps directly to summary §6.

### 4.2. `ReturnStdDev`

Standard deviation of daily returns within the window. Enables risk-adjusted ranking later (step 5).

Approach: SQL — use `STDDEV_SAMP` over `(close_price - prev_close) / prev_close` per symbol within the window. Same CTE pattern as `PercentPositiveDays`.

Maps to summary §7. Distinct from CPVI: stddev measures dispersion of returns, CPVI measures path inefficiency.

### 4.3. `MaxDrawdown`

Peak-to-trough decline within the window, expressed as a percentage. Interpretable risk metric tied directly to summary §5 (crash mechanics).

Approach: SQL — running max via window functions, then `(running_max - current) / running_max`, take the max of that.

### 4.4. `Momentum_12_1` *(larger design decision)*

The academic-standard 12-month-return-skip-most-recent-month signal. Two implementation options to discuss before building:

- **Option A — Parameterise the existing window.** Add a `SkipRecentMonth: bool` field on `StartStatisticJobParameters`. When set, the SQL shifts the effective end date back by ~21 trading days for the growth calculation only.
- **Option B — Separate column.** Always compute the standard 12-1 metric over the trailing 12 months, regardless of the user's chosen window. Gives a stable "academic momentum" reading independent of the exploration window.

Option B is more aligned with the literature (the 12-1 signal is *defined* by its fixed window), but Option A is simpler and reuses existing infrastructure. Recommend Option B if we want to compare different windows side-by-side; Option A if we want the existing endpoint to stay backwards-compatible.

### 4.5. `Beta` *(deferred — needs benchmark data)*

Beta vs. a benchmark index (S&P 500 for NYSE/NASDAQ, MERVAL for CEDEAR?). Required for Gray's "boring vs. lottery" filter — low beta is the proxy for "boring," which historically outperforms.

**Open question before committing:** does the database currently hold a benchmark series? If not, this requires adding a loader job similar to `LoadDailyNYSEPrices/`. Defer until that is confirmed.

### 4.6. `IsInMomentum` *(implemented — Option B with project-owner thresholds)*

Boolean flag combining the relative-ranking idea from the literature with the quality gates from §6/§7 of `momentum_trading_summary.md`.

**Top-growth endpoint** (`GetTopGrowth`): true when the row is in the **top quintile** of `PercentageChange` within the post-threshold result set (`NTILE(5) OVER (ORDER BY percentageChange DESC) = 1`) AND `PercentPositiveDays >= 50` AND `MaxDrawdown <= 25`. The NTILE is evaluated after WHERE, so the quintile is over rows that already pass the user's `MinimumExpectedGrowth`.

**Watchlist endpoint** (`GetWatchlistGroupAsync`): true when `PercentageChange >= 20` AND `PercentPositiveDays >= 50` AND `MaxDrawdown <= 25`. Relative ranking is skipped because watchlists are small (3–10 symbols) and `NTILE(5)` would be degenerate.

**Threshold notes — these are project-owner decisions, not from the literature:**

- **Smoothness floor at 50% (not 55%).** The literature (Gray/Vogel §6) doesn't pin a specific cutoff — only "higher is better." 55% was an initial heuristic and was filtering out clearly-trending stocks the project owner could see by eye. **50% was chosen by the project owner** as the floor: above 50% means more up-days than down-days, which is the natural meaningful boundary. To be revisited if it lets in too many noise / gap-driven names.
- **Max drawdown ≤ 25%.** Caps choppy / crash-and-recover patterns. Tunable.
- **Top quintile (not decile).** The literature commonly uses deciles for full-universe momentum. Quintile is used here because the `GetTopGrowth` result set is already pre-filtered by `MinimumExpectedGrowth` — taking a tighter cut on an already-filtered set would leave very few flagged rows. Will need revisiting if/when Option C (full-exchange ranking via `Momentum_12_1`) is implemented.
- **Watchlist `PercentageChange >= 20` floor.** "Did something good happen" gate; window-dependent (20% is huge over 1 month, modest over 1 year). Known limitation.

Caveats: the boolean is lossier than the literature's continuous scores, and "in momentum" here means "in momentum within the queried set," not "in momentum across the entire exchange."

---

## 5. Existing-column recommendations

- **`Rsi`**: keep, but reframe its purpose. It's a screen for current overbought/oversold state, not a momentum signal. Any UI label or API doc that suggests otherwise should be reworded. Optionally: scope the calculation to the user's analysis window if we want "RSI within the window" rather than "current RSI" — but this is a behaviour change and should be discussed.
- **`Volatility` (CPVI)**: keep, but pair it with `ReturnStdDev` (§4.2). CPVI is a defensible smoothness proxy; it should not be the only volatility-ish number we expose, and it should not be the basis for risk scaling.
- **Ranking**: optionally add a `RiskAdjustedScore` sort (`PercentageChange / ReturnStdDev`) once `ReturnStdDev` exists. This is the institutional standard per summary §7. Make it **opt-in via a parameter**, not the default — changing default sort order changes existing behaviour for every consumer.

---

## 6. Touch points

Any new column in `SymbolResult` requires changes in two places due to current SQL duplication:

- `Services/StatisticsService.cs::GetTopGrowth` (lines 12-129)
- `Services/StatisticsService.cs::GetWatchlistGroupAsync` (lines 152-275)

Other files affected:

- `Models/SymbolResult.cs` — DTO definition
- `Models/StartStatisticJobParameters.cs` — new fields if 12-1 or risk-adjusted ranking become parameters
- `Calculators/` — only if a new metric is too complex for inline SQL (most of these aren't; mirror the `CpviCalculator.cs` pattern if needed)

**Out of scope for this plan:** frontend changes in `board/`. Any new column will eventually need UI surfacing, but that is a separate task once the backend additions are stable.

**Refactor opportunity (not blocking):** the SQL skeleton in `GetTopGrowth` and `GetWatchlistGroupAsync` is duplicated. After 2–3 column additions this duplication becomes annoying. A shared CTE-builder or a stored function is worth considering, but should be its own change, not bundled with the column work.

---

## 7. Open decisions

1. **`Momentum_12_1` design** — Option A (parameterise existing window) or Option B (separate fixed-window column)? See §4.4.
2. **Benchmark series for `Beta`** — does the database already have S&P 500 / NASDAQ Composite / MERVAL daily closes? If not, are we willing to add a loader? See §4.5.
3. **RSI scope** — leave as "all-history current state" or scope to the analysis window? See §5.
4. **Risk-adjusted ranking** — add as opt-in sort or make it the default? Recommend opt-in. See §5.

---

## 8. Verification (when implementation begins)

1. `dotnet build` and `dotnet test` from inside `growy_server/`.
2. `POST /statistics/start` with a known date range and exchange; poll `GET /statistics/status/{jobId}` and inspect the new columns in the result payload.
3. Spot-check 2–3 symbols by hand: pick a known steady climber and a known choppy mover, confirm `PercentPositiveDays` and `ReturnStdDev` rank them in the expected order.
4. Confirm the unix-date-in-milliseconds convention (CLAUDE.md) is preserved in any new SQL using `unix_date`.

---

## 9. Sources

- [`docs/momentum_trading_summary.md`](./momentum_trading_summary.md) — the consolidated literature this plan is measured against
- [`docs/JPM Fact Fiction and Momentum Investing.md`](./JPM%20Fact%20Fiction%20and%20Momentum%20Investing.md) — the primary academic source behind the summary
- `Services/StatisticsService.cs`, `Models/SymbolResult.cs`, `Calculators/` — the current implementation surface
