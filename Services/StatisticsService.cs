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

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            string query = $@"
                WITH precios_filtrados AS (
                  SELECT
                    symbol,
                    close_price,
                    unix_date,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date ASC) AS rn_asc,
                    ROW_NUMBER() OVER (PARTITION BY symbol ORDER BY unix_date DESC) AS rn_desc
                  FROM {tableName}
                  WHERE unix_date BETWEEN @StartDate AND @EndDate {exchangeFilter}
                ),
                precios_inicio AS (
                  SELECT symbol, close_price AS precio_inicio
                  FROM precios_filtrados
                  WHERE rn_asc = 1
                ),
                precios_fin AS (
                  SELECT symbol, close_price AS precio_fin
                  FROM precios_filtrados
                  WHERE rn_desc = 1
                ),
                crecimientos AS (
                  SELECT
                    i.symbol AS symbol,
                    ((f.precio_fin - i.precio_inicio) / i.precio_inicio) * 100 AS percentageChange,
                    i.precio_inicio AS oldestPrice,
                    f.precio_fin AS newestPrice
                  FROM precios_inicio i
                  JOIN precios_fin f ON i.symbol = f.symbol
                  WHERE i.precio_inicio <> 0
                )
                SELECT
                    c.symbol,
                    c.percentageChange,
                    c.oldestPrice,
                    c.newestPrice,
                    co.analyst_target_price,
                    co.eps,
                    co.market_capitalization,
                    co.description,
                    co.sector,
                    co.industry,
                    co.company_name
                FROM crecimientos c
                LEFT JOIN companies co ON co.symbol = c.symbol
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
                    });
                }
            }

            jobInfo.ProcessingMessage = "Computing volatility";
            var symbolNames = symbols.Select(x => x.Symbol).ToArray();
            var cpviResults = await CpviCalculator.CalculateAsync(symbolNames, tableName, connection,
                startJobParameters.StartUnixDate * 1000, startJobParameters.EndUnixDate * 1000,
                isCedear ? null : startJobParameters.Exchange, cancellationToken);
            var cpviMap = cpviResults.ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var s in symbols)
                if (cpviMap.TryGetValue(s.Symbol, out var cpvi))
                    s.Volatility = cpvi;

            jobInfo.ProcessingMessage = "Computing RSI";
            var rsiResults = await RsiCalculator.CalculateAsync(symbolNames, tableName, connection, cancellationToken: cancellationToken);
            var rsiMap = rsiResults.ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbols)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            return symbols;
        }

        public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, string exchange, CancellationToken cancellationToken = default)
        {
            var (tableName, isCedear, _) = ResolveTable(exchange);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            string exchangeClause = isCedear ? "" : "AND exchange = @exchange";
            string query = $@"
                SELECT close_price, unix_date
                FROM {tableName}
                WHERE symbol = @symbol {exchangeClause}
                ORDER BY unix_date ASC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@symbol", symbol);
            if (!isCedear)
                command.Parameters.AddWithValue("@exchange", exchange);

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
