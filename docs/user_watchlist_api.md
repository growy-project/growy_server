# User Watchlist API (`/my-list`)

Per-user watchlist of saved symbols. All endpoints require a valid JWT (`Authorization: Bearer <token>`) issued by `POST /auth/google-login`. The user is identified by the `sub` claim of that JWT.

**Hard limit:** each user may save at most **20 symbols**.

---

## `POST /my-list/symbol`

Save a symbol to the current user's watchlist.

### Request

```http
POST /my-list/symbol
Authorization: Bearer <jwt>
Content-Type: application/json

{
  "symbol": "AAPL",
  "exchange": "NASDAQ"
}
```

| Field | Type | Required | Notes |
|---|---|---|---|
| `symbol` | string | yes | Ticker symbol (e.g. `AAPL`, `GGAL`). |
| `exchange` | string | yes | One of `NYSE`, `NASDAQ`, `CEDEAR`. The same symbol may exist on multiple exchanges and is treated as distinct rows. |

### Responses

| Status | When | Body |
|---|---|---|
| `201 Created` | Symbol added. | empty |
| `400 Bad Request` | Body validation failed. | `{ "errors": ... }` |
| `401 Unauthorized` | Missing or invalid JWT. | empty |
| `409 Conflict` | Either the symbol+exchange pair is already in the watchlist, or the user already has 20 symbols saved. | `{ "message": "Watchlist limit reached (20 symbols)" }` or `{ "message": "Symbol AAPL (NASDAQ) is already in your watchlist" }` |

### Notes

- The 20-cap and duplicate check run inside a serializable transaction, so concurrent POSTs from two tabs cannot exceed the limit or create duplicates.

---

## `DELETE /my-list/symbol`

Remove a symbol from the current user's watchlist.

### Request

```http
DELETE /my-list/symbol?symbol=AAPL&exchange=NASDAQ
Authorization: Bearer <jwt>
```

| Query | Type | Required | Notes |
|---|---|---|---|
| `symbol` | string | yes | Must match an existing row exactly. |
| `exchange` | string | yes | Must match the same exchange the row was saved with. |

### Responses

| Status | When | Body |
|---|---|---|
| `204 No Content` | Row deleted. | empty |
| `400 Bad Request` | `symbol` or `exchange` missing. | `{ "message": "symbol and exchange are required" }` |
| `401 Unauthorized` | Missing or invalid JWT. | empty |
| `404 Not Found` | No such row for this user. | empty |

---

## `GET /my-list`

Kick off a background job that computes per-symbol statistics for **every** symbol in the current user's watchlist (mixed exchanges). Returns a `jobId`; poll `GET /statistics/status/{jobId}` for progress and the final result.

The result shape is identical to `POST /statistics/start` — same `List<SymbolResult>` (`PercentageChange`, `Rsi`, `Volatility`, fundamentals, etc.). Unlike top-growth, **no `MinimumExpectedGrowth` threshold is applied** — every saved symbol is returned regardless of performance.

### Request

```http
GET /my-list?startUnixDate=1735689600&endUnixDate=1743465600
Authorization: Bearer <jwt>
```

| Query | Type | Required | Notes |
|---|---|---|---|
| `startUnixDate` | long | yes | Unix seconds. Start of the analysis window. |
| `endUnixDate` | long | yes | Unix seconds. End of the analysis window. |

### Responses

| Status | When | Body |
|---|---|---|
| `200 OK` | Job created. | `{ "jobId": "<guid>" }` |
| `401 Unauthorized` | Missing or invalid JWT. | empty |

### Polling

Use the existing status endpoint:

```http
GET /statistics/status/{jobId}
```

Returns a `StatisticJobInfo`:

```json
{
  "startJobParameters": null,
  "autoClearAfterStatusJobCheck": true,
  "jobId": "...",
  "errors": null,
  "status": 2,
  "result": [
    {
      "symbol": "AAPL",
      "exchange": "NASDAQ",
      "percentageChange": 12.4,
      "oldestPrice": 175.2,
      "newestPrice": 197.0,
      "marketCapitalization": 3000000000000,
      "eps": 6.42,
      "targetPrice": 220.0,
      "rsi": 58.1,
      "volatility": 1.24,
      "companyName": "Apple Inc.",
      "description": "...",
      "sector": "Technology",
      "industry": "Consumer Electronics"
    }
  ],
  "percentComplete": 100,
  "processingMessage": "Computing statistics for 7 watchlist symbols",
  "currentPage": 1
}
```

`status` enum values: `0 = NotStarted`, `1 = InProgress`, `2 = Completed`, `3 = CompletedWithErrors`.

For watchlist jobs, `startJobParameters` is `null` (top-growth is the only variant that populates it). Jobs auto-clear from the in-memory cache 5 minutes after the last status check.

---

## End-to-end example

```bash
JWT="$(curl -s -X POST http://localhost:7138/auth/google-login \
  -H 'Content-Type: application/json' \
  -d '{"idToken":"<google-id-token>"}' | jq -r .token)"

# Save two symbols
curl -X POST http://localhost:7138/my-list/symbol \
  -H "Authorization: Bearer $JWT" -H 'Content-Type: application/json' \
  -d '{"symbol":"AAPL","exchange":"NASDAQ"}'

curl -X POST http://localhost:7138/my-list/symbol \
  -H "Authorization: Bearer $JWT" -H 'Content-Type: application/json' \
  -d '{"symbol":"GGAL","exchange":"CEDEAR"}'

# Run the watchlist job
JOB_ID="$(curl -s -G "http://localhost:7138/my-list" \
  --data-urlencode "startUnixDate=1735689600" \
  --data-urlencode "endUnixDate=1743465600" \
  -H "Authorization: Bearer $JWT" | jq -r .jobId)"

# Poll until complete
curl -s "http://localhost:7138/statistics/status/$JOB_ID" \
  -H "Authorization: Bearer $JWT" | jq

# Remove a symbol
curl -X DELETE "http://localhost:7138/my-list/symbol?symbol=AAPL&exchange=NASDAQ" \
  -H "Authorization: Bearer $JWT"
```
