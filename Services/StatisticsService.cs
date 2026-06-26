using growy_server.Calculators;
using growy_server.Models;
using Npgsql;

namespace growy_server.Services
{
    public class StatisticsService(IConfiguration configuration) : IStatisticsService
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        public async Task<List<SymbolResult>> GetTopGrowth(StartStatisticJobParameters startJobParameters, StatisticJobInfo jobInfo, CancellationToken cancellationToken = default)
        {
            var (tableName, isCedear, exchangeFilter) = ResolveTable(startJobParameters.Exchange);

            // ProcessingMessage broadcasts the current phase to the polling endpoint.
            // Cheap inline updates only — IsInMomentum is computed in the same SQL pass
            // via NTILE, so no separate phase is needed (see docs/metrics_improvement_plan.md).
            jobInfo.ProcessingMessage = startJobParameters.Exchange switch
            {
                "NASDAQ" => "Retrieving statistics from 4000+ Nasdaq tickers and classifying momentum",
                "NYSE" => "Retrieving statistics from 2000+ NYSE tickers and classifying momentum",
                "CEDEAR" => "Filtering Nasdaq and NYSE companies with CEDEARs and classifying momentum",
                _ => jobInfo.ProcessingMessage,
            };

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // SQL builds price-derived metrics (smoothness, stddev, drawdown) then computes
            // IsInMomentum inline via NTILE: top quintile of percentageChange (post-WHERE)
            // gated by smoothness >= 50% (project-owner choice — see metrics_improvement_plan.md §4.6)
            // and max drawdown <= 25%.
            // Strategy: docs/momentum_trading_summary.md §6 + §7.
            // Design: docs/metrics_improvement_plan.md (Option B).
            string query = $@"
                WITH filtered_prices AS (
                  SELECT
                    symbol,
                    close_price,
                    unix_date,
                    LAG(close_price) OVER (PARTITION BY symbol ORDER BY unix_date ASC) AS prev_close,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date ASC) AS rn_asc,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date DESC) AS rn_desc
                  FROM {tableName}
                  WHERE unix_date BETWEEN @StartDate AND @EndDate {exchangeFilter}
                ),
                start_prices AS (
                  SELECT symbol, close_price AS start_price
                  FROM filtered_prices
                  WHERE rn_asc = 1
                ),
                end_prices AS (
                  SELECT symbol, close_price AS end_price
                  FROM filtered_prices
                  WHERE rn_desc = 1
                ),
                quality_metrics AS (
                  SELECT
                    symbol,
                    (COUNT(*) FILTER (WHERE close_price > prev_close))::float / COUNT(*) * 100 AS percent_positive_days
                  FROM filtered_prices
                  WHERE prev_close IS NOT NULL
                  GROUP BY symbol
                ),
                return_stats AS (
                  SELECT
                    symbol,
                    STDDEV_SAMP((close_price - prev_close) / NULLIF(prev_close, 0)) * 100 AS return_std_dev
                  FROM filtered_prices
                  WHERE prev_close IS NOT NULL
                  GROUP BY symbol
                ),
                drawdown_metrics AS (
                  SELECT
                    symbol,
                    MAX((running_max - close_price) / NULLIF(running_max, 0)) * 100 AS max_drawdown
                  FROM (
                    SELECT
                      symbol,
                      close_price,
                      MAX(close_price) OVER (PARTITION BY symbol ORDER BY unix_date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_max
                    FROM filtered_prices
                  ) peaks
                  GROUP BY symbol
                ),
                growth AS (
                  SELECT
                    s.symbol AS symbol,
                    ((e.end_price - s.start_price) / s.start_price) * 100 AS percentageChange,
                    s.start_price AS oldestPrice,
                    e.end_price AS newestPrice
                  FROM start_prices s
                  JOIN end_prices e ON s.symbol = e.symbol
                  WHERE s.start_price <> 0
                )
                SELECT
                    g.symbol,
                    g.percentageChange,
                    g.oldestPrice,
                    g.newestPrice,
                    co.analyst_target_price,
                    co.eps,
                    co.market_capitalization,
                    co.description,
                    co.sector,
                    co.industry,
                    co.company_name,
                    co.exchange,
                    qm.percent_positive_days,
                    rs.return_std_dev,
                    dm.max_drawdown,
                    (NTILE(5) OVER (ORDER BY g.percentageChange DESC) = 1
                     AND COALESCE(qm.percent_positive_days, 0) >= 50
                     AND COALESCE(dm.max_drawdown, 0) <= 25) AS is_in_momentum
                FROM growth g
                LEFT JOIN companies co ON co.symbol = g.symbol
                LEFT JOIN quality_metrics qm ON qm.symbol = g.symbol
                LEFT JOIN return_stats rs ON rs.symbol = g.symbol
                LEFT JOIN drawdown_metrics dm ON dm.symbol = g.symbol
                WHERE percentageChange > @Threshold
                ORDER BY percentageChange DESC;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@StartDate", startJobParameters.StartUnixDate * 1000);
            command.Parameters.AddWithValue("@EndDate", startJobParameters.EndUnixDate * 1000);
            command.Parameters.AddWithValue("@Threshold", startJobParameters.MinimumExpectedGrowth);

            if (!isCedear)
                command.Parameters.AddWithValue("@Exchange", startJobParameters.Exchange);

            var symbols = new List<SymbolResult>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    symbols.Add(new SymbolResult
                    {
                        Symbol = reader.GetString(0),
                        PercentageChange = reader.GetDouble(1),
                        OldestPrice = reader.GetDouble(2),
                        NewestPrice = reader.GetDouble(3),
                        TargetPrice = reader.IsDBNull(4) ? 0 : (double)reader.GetDecimal(4),
                        Eps = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                        MarketCapitalization = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                        Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Sector = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Industry = reader.IsDBNull(9) ? null : reader.GetString(9),
                        CompanyName = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Exchange = reader.IsDBNull(11) ? null : reader.GetString(11),
                        PercentPositiveDays = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                        ReturnStdDev = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                        MaxDrawdown = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                        IsInMomentum = !reader.IsDBNull(15) && reader.GetBoolean(15),
                    });
                }
            }

            jobInfo.ProcessingMessage = "Computing RSI and Volatility";
            var symbolNames = symbols.Select(x => x.Symbol).ToArray();
            var targetMap = symbols.ToDictionary(s => s.Symbol, s => s.TargetPrice);

            await using var rsiConnection = new NpgsqlConnection(_connectionString);
            await rsiConnection.OpenAsync(cancellationToken);

            await using var bounceConnection = new NpgsqlConnection(_connectionString);
            await bounceConnection.OpenAsync(cancellationToken);

            var cpviTask = CpviCalculator.CalculateAsync(symbolNames, tableName, connection,
                startJobParameters.StartUnixDate * 1000, startJobParameters.EndUnixDate * 1000,
                isCedear ? null : startJobParameters.Exchange, cancellationToken);
            var rsiTask = RsiCalculator.CalculateAsync(symbolNames, tableName, rsiConnection, cancellationToken: cancellationToken);
            var bounceTask = BounceCalculator.CalculateAsync(symbolNames, tableName, bounceConnection, targetMap,
                startJobParameters.StartUnixDate * 1000, startJobParameters.EndUnixDate * 1000,
                isCedear ? null : startJobParameters.Exchange, cancellationToken);

            await Task.WhenAll(cpviTask, rsiTask, bounceTask);

            var cpviMap = (await cpviTask).ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var s in symbols)
                if (cpviMap.TryGetValue(s.Symbol, out var cpvi))
                    s.Volatility = cpvi;

            var rsiMap = (await rsiTask).ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbols)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            var bounceMap = (await bounceTask).ToDictionary(b => b.Symbol, b => b.IsBouncing);
            foreach (var s in symbols)
                if (bounceMap.TryGetValue(s.Symbol, out var bouncing))
                    s.IsBouncing = bouncing;

            return symbols;
        }

        public async Task<List<SymbolResult>> GetWatchlistStatistics(List<(string Symbol, string Exchange)> entries, long startUnixDate, long endUnixDate, StatisticJobInfo jobInfo, CancellationToken cancellationToken = default)
        {
            if (entries.Count == 0)
                return new List<SymbolResult>();

            jobInfo.ProcessingMessage = $"Computing statistics for {entries.Count} watchlist symbols";

            var results = new List<SymbolResult>();

            foreach (var group in entries.GroupBy(e => e.Exchange))
            {
                var symbols = group.Select(e => e.Symbol).Distinct().ToArray();
                if (symbols.Length == 0) continue;

                var groupResults = await GetWatchlistGroupAsync(symbols, group.Key, startUnixDate, endUnixDate, cancellationToken);
                results.AddRange(groupResults);
            }

            return results.OrderByDescending(r => r.PercentageChange).ToList();
        }

        private async Task<List<SymbolResult>> GetWatchlistGroupAsync(string[] symbols, string exchange, long startUnixDate, long endUnixDate, CancellationToken cancellationToken)
        {
            var (tableName, isCedear, _) = ResolveTable(exchange);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var paramPlaceholders = new List<string>();
            var symbolParams = new List<NpgsqlParameter>();
            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                symbolParams.Add(new NpgsqlParameter($"@p{i}", symbols[i]));
            }
            string inClause = string.Join(", ", paramPlaceholders);
            string exchangeFilter = isCedear ? "" : "AND exchange = @Exchange";

            // Watchlist uses absolute thresholds for IsInMomentum (Option B/B1):
            // percentageChange >= 20%, smoothness >= 50% (project-owner choice — see
            // metrics_improvement_plan.md §4.6), max drawdown <= 25%.
            // Relative top-quintile ranking is skipped — it would be degenerate on small watchlists.
            // Strategy: docs/momentum_trading_summary.md §6 + §7.
            // Design: docs/metrics_improvement_plan.md (Option B).
            string query = $@"
                WITH filtered_prices AS (
                  SELECT
                    symbol,
                    close_price,
                    unix_date,
                    LAG(close_price) OVER (PARTITION BY symbol ORDER BY unix_date ASC) AS prev_close,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date ASC) AS rn_asc,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date DESC) AS rn_desc
                  FROM {tableName}
                  WHERE unix_date BETWEEN @StartDate AND @EndDate
                    AND symbol IN ({inClause})
                    {exchangeFilter}
                ),
                start_prices AS (
                  SELECT symbol, close_price AS start_price
                  FROM filtered_prices
                  WHERE rn_asc = 1
                ),
                end_prices AS (
                  SELECT symbol, close_price AS end_price
                  FROM filtered_prices
                  WHERE rn_desc = 1
                ),
                quality_metrics AS (
                  SELECT
                    symbol,
                    (COUNT(*) FILTER (WHERE close_price > prev_close))::float / COUNT(*) * 100 AS percent_positive_days
                  FROM filtered_prices
                  WHERE prev_close IS NOT NULL
                  GROUP BY symbol
                ),
                return_stats AS (
                  SELECT
                    symbol,
                    STDDEV_SAMP((close_price - prev_close) / NULLIF(prev_close, 0)) * 100 AS return_std_dev
                  FROM filtered_prices
                  WHERE prev_close IS NOT NULL
                  GROUP BY symbol
                ),
                drawdown_metrics AS (
                  SELECT
                    symbol,
                    MAX((running_max - close_price) / NULLIF(running_max, 0)) * 100 AS max_drawdown
                  FROM (
                    SELECT
                      symbol,
                      close_price,
                      MAX(close_price) OVER (PARTITION BY symbol ORDER BY unix_date ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS running_max
                    FROM filtered_prices
                  ) peaks
                  GROUP BY symbol
                ),
                growth AS (
                  SELECT
                    s.symbol AS symbol,
                    ((e.end_price - s.start_price) / s.start_price) * 100 AS percentageChange,
                    s.start_price AS oldestPrice,
                    e.end_price AS newestPrice
                  FROM start_prices s
                  JOIN end_prices e ON s.symbol = e.symbol
                  WHERE s.start_price <> 0
                )
                SELECT
                    g.symbol,
                    g.percentageChange,
                    g.oldestPrice,
                    g.newestPrice,
                    co.analyst_target_price,
                    co.eps,
                    co.market_capitalization,
                    co.description,
                    co.sector,
                    co.industry,
                    co.company_name,
                    co.exchange,
                    qm.percent_positive_days,
                    rs.return_std_dev,
                    dm.max_drawdown,
                    (g.percentageChange >= 20
                     AND COALESCE(qm.percent_positive_days, 0) >= 50
                     AND COALESCE(dm.max_drawdown, 0) <= 25) AS is_in_momentum
                FROM growth g
                LEFT JOIN companies co ON co.symbol = g.symbol
                LEFT JOIN quality_metrics qm ON qm.symbol = g.symbol
                LEFT JOIN return_stats rs ON rs.symbol = g.symbol
                LEFT JOIN drawdown_metrics dm ON dm.symbol = g.symbol
                ORDER BY percentageChange DESC;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@StartDate", startUnixDate * 1000);
            command.Parameters.AddWithValue("@EndDate", endUnixDate * 1000);
            foreach (var p in symbolParams)
                command.Parameters.Add(p);
            if (!isCedear)
                command.Parameters.AddWithValue("@Exchange", exchange);

            var symbolResults = new List<SymbolResult>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                {
                    symbolResults.Add(new SymbolResult
                    {
                        Symbol = reader.GetString(0),
                        PercentageChange = reader.GetDouble(1),
                        OldestPrice = reader.GetDouble(2),
                        NewestPrice = reader.GetDouble(3),
                        TargetPrice = reader.IsDBNull(4) ? 0 : (double)reader.GetDecimal(4),
                        Eps = reader.IsDBNull(5) ? null : reader.GetDecimal(5),
                        MarketCapitalization = reader.IsDBNull(6) ? null : reader.GetDecimal(6),
                        Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                        Sector = reader.IsDBNull(8) ? null : reader.GetString(8),
                        Industry = reader.IsDBNull(9) ? null : reader.GetString(9),
                        CompanyName = reader.IsDBNull(10) ? null : reader.GetString(10),
                        Exchange = reader.IsDBNull(11) ? null : reader.GetString(11),
                        PercentPositiveDays = reader.IsDBNull(12) ? 0 : reader.GetDouble(12),
                        ReturnStdDev = reader.IsDBNull(13) ? 0 : reader.GetDouble(13),
                        MaxDrawdown = reader.IsDBNull(14) ? 0 : reader.GetDouble(14),
                        IsInMomentum = !reader.IsDBNull(15) && reader.GetBoolean(15),
                    });
                }
            }

            if (symbolResults.Count == 0)
                return symbolResults;

            var symbolNames = symbolResults.Select(x => x.Symbol).ToArray();
            var targetMap = symbolResults.ToDictionary(s => s.Symbol, s => s.TargetPrice);

            await using var rsiConnection = new NpgsqlConnection(_connectionString);
            await rsiConnection.OpenAsync(cancellationToken);

            await using var bounceConnection = new NpgsqlConnection(_connectionString);
            await bounceConnection.OpenAsync(cancellationToken);

            var cpviTask = CpviCalculator.CalculateAsync(symbolNames, tableName, connection,
                startUnixDate * 1000, endUnixDate * 1000,
                isCedear ? null : exchange, cancellationToken);
            var rsiTask = RsiCalculator.CalculateAsync(symbolNames, tableName, rsiConnection, cancellationToken: cancellationToken);
            var bounceTask = BounceCalculator.CalculateAsync(symbolNames, tableName, bounceConnection, targetMap,
                startUnixDate * 1000, endUnixDate * 1000,
                isCedear ? null : exchange, cancellationToken);

            await Task.WhenAll(cpviTask, rsiTask, bounceTask);

            var cpviMap = (await cpviTask).ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var s in symbolResults)
                if (cpviMap.TryGetValue(s.Symbol, out var cpvi))
                    s.Volatility = cpvi;

            var rsiMap = (await rsiTask).ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbolResults)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            var bounceMap = (await bounceTask).ToDictionary(b => b.Symbol, b => b.IsBouncing);
            foreach (var s in symbolResults)
                if (bounceMap.TryGetValue(s.Symbol, out var bouncing))
                    s.IsBouncing = bouncing;

            return symbolResults;
        }

        public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, GetSymbolHistoryParameters parameters, CancellationToken cancellationToken = default)
        {
            var (tableName, isCedear, _) = ResolveTable(parameters.Exchange);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            string exchangeClause = isCedear ? "" : "AND exchange = @exchange";
            string startDateClause = parameters.StartUnixDate.HasValue ? "AND unix_date >= @startUnixDate" : "";
            string endDateClause = parameters.EndUnixDate.HasValue ? "AND unix_date <= @endUnixDate" : "";
            string query = $@"
                SELECT close_price, unix_date
                FROM {tableName}
                WHERE symbol = @symbol {exchangeClause} {startDateClause} {endDateClause}
                ORDER BY unix_date ASC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@symbol", symbol);
            if (!isCedear)
                command.Parameters.AddWithValue("@exchange", parameters.Exchange);
            if (parameters.StartUnixDate.HasValue)
                command.Parameters.AddWithValue("@startUnixDate", parameters.StartUnixDate.Value * 1000);
            if (parameters.EndUnixDate.HasValue)
                command.Parameters.AddWithValue("@endUnixDate", parameters.EndUnixDate.Value * 1000);

            var prices = new List<PriceEntry>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                prices.Add(new PriceEntry
                {
                    ClosePrice = reader.GetDouble(0),
                    UnixDate = reader.GetInt64(1)
                });
            }

            return new SymbolHistoryResult { Symbol = symbol, Prices = prices, Ema20 = EmaCalculator.Calculate20Ema(prices) };
        }

        private static (string TableName, bool IsCedear, string ExchangeFilter) ResolveTable(string exchange)
        {
            bool isCedear = exchange == "CEDEAR";
            string tableName = isCedear ? "symbol_date_price_cedears" : "symbol_date_price";
            string exchangeFilter = isCedear ? "" : "AND exchange = @Exchange";
            return (tableName, isCedear, exchangeFilter);
        }
    }
}
