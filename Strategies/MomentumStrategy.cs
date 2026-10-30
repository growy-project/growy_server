using growy_server.Calculators;
using growy_server.Models;
using growy_server.Services;
using Npgsql;
using NpgsqlTypes;

namespace growy_server.Strategies
{
    // Top-growth screener: price growth over a date range plus quality metrics, RSI and bounce.
    // Serves both the exchange-wide scan (POST /statistics/start) and the watchlist (symbol list).
    public class MomentumStrategy(IConfiguration configuration) : IScreeningStrategy<MomentumRequest, SymbolResult>
    {
        public const string StrategyKey = "momentum";

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        public string Key => StrategyKey;

        public async Task<List<SymbolResult>> ScreenAsync(MomentumRequest request, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            bool filterSymbols = request.Symbols is not null;
            if (filterSymbols && request.Symbols!.Count == 0)
                return new List<SymbolResult>();

            var (tableName, isCedear, _) = PriceTables.Resolve(request.Exchange);

            // The per-exchange message only describes a whole-exchange scan.
            if (!filterSymbols)
            {
                var message = request.Exchange switch
                {
                    "NASDAQ" => "Retrieving statistics from 4000+ Nasdaq tickers",
                    "NYSE" => "Retrieving statistics from 2000+ NYSE tickers",
                    "CEDEAR" => "Filtering Nasdaq and NYSE companies with CEDEARs",
                    _ => null,
                };
                if (message is not null)
                    progress?.Report(message);
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            string query = BuildQuery(tableName, isCedear, filterSymbols, request.MinimumExpectedGrowth.HasValue);

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@StartDate", request.StartUnixDate * 1000);
            command.Parameters.AddWithValue("@EndDate", request.EndUnixDate * 1000);
            if (filterSymbols)
                command.Parameters.Add(new NpgsqlParameter("@Symbols", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = request.Symbols!.ToArray() });
            if (request.MinimumExpectedGrowth.HasValue)
                command.Parameters.AddWithValue("@Threshold", request.MinimumExpectedGrowth.Value);
            if (!isCedear)
                command.Parameters.AddWithValue("@Exchange", request.Exchange);

            var symbols = new List<SymbolResult>();
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
            {
                while (await reader.ReadAsync(cancellationToken))
                    symbols.Add(ReadSymbolResult(reader));
            }

            progress?.Report("Computing RSI");
            await EnrichWithCalculatorsAsync(symbols, tableName, connection,
                request.StartUnixDate * 1000, request.EndUnixDate * 1000,
                isCedear ? null : request.Exchange, cancellationToken);

            return symbols;
        }

        // SQL builds price-derived quality metrics (smoothness, stddev, drawdown)
        // alongside growth in a single pass. The symbol filter (watchlist) and the growth
        // threshold (exchange-wide scan) are the only optional parts.
        public static string BuildQuery(string tableName, bool isCedear, bool filterSymbols, bool applyThreshold)
        {
            string exchangeFilter = isCedear ? "" : "AND exchange = @Exchange";
            string symbolFilter = filterSymbols ? "AND symbol = ANY(@Symbols)" : "";
            string thresholdFilter = applyThreshold ? "WHERE percentageChange > @Threshold" : "";

            return $@"
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
                    {symbolFilter}
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
                    dm.max_drawdown
                FROM growth g
                LEFT JOIN companies co ON co.symbol = g.symbol
                LEFT JOIN quality_metrics qm ON qm.symbol = g.symbol
                LEFT JOIN return_stats rs ON rs.symbol = g.symbol
                LEFT JOIN drawdown_metrics dm ON dm.symbol = g.symbol
                {thresholdFilter}
                ORDER BY percentageChange DESC;";
        }

        // Reads the 15-column projection produced by BuildQuery. These ordinals are the
        // contract with the query's final SELECT list — reordering columns there requires
        // updating them here.
        private static SymbolResult ReadSymbolResult(NpgsqlDataReader reader) => new()
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
        };

        // Fills in Rsi and IsBouncing on results already carrying the
        // SQL-derived metrics. Date bounds are in milliseconds, matching the DB.
        private async Task EnrichWithCalculatorsAsync(
            List<SymbolResult> symbols, string tableName, NpgsqlConnection connection,
            long startUnixMs, long endUnixMs, string? exchange, CancellationToken cancellationToken)
        {
            if (symbols.Count == 0)
                return;

            var symbolNames = symbols.Select(x => x.Symbol).ToArray();
            var targetMap = symbols.ToDictionary(s => s.Symbol, s => s.TargetPrice);

            await using var rsiConnection = new NpgsqlConnection(_connectionString);
            await rsiConnection.OpenAsync(cancellationToken);

            // Bounce needs the windowed rows; RSI runs concurrently on its own
            // connection over full history.
            var priceSeriesTask = PriceSeriesLoader.LoadAsync(symbolNames, tableName, connection,
                startUnixMs, endUnixMs, exchange, cancellationToken);
            var rsiTask = RsiCalculator.CalculateAsync(symbolNames, tableName, rsiConnection,
                exchange: exchange, cancellationToken: cancellationToken);

            await Task.WhenAll(priceSeriesTask, rsiTask);

            var priceSeries = await priceSeriesTask;

            var rsiMap = (await rsiTask).ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbols)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            var bounceMap = BounceCalculator.CalculateFromSeries(priceSeries, targetMap).ToDictionary(b => b.Symbol, b => b.IsBouncing);
            foreach (var s in symbols)
                if (bounceMap.TryGetValue(s.Symbol, out var bouncing))
                    s.IsBouncing = bouncing;
        }
    }
}
