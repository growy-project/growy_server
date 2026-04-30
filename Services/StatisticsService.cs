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

            jobInfo.ProcessingMessage = startJobParameters.Exchange switch
            {
                "NASDAQ" => "Retrieving statistics from 4000+ Nasdaq tickers",
                "NYSE" => "Retrieving statistics from 2000+ NYSE tickers",
                "CEDEAR" => "Filtering Nasdaq and NYSE companies with CEDEARs",
                _ => jobInfo.ProcessingMessage,
            };

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            string query = $@"
                WITH filtered_prices AS (
                  SELECT
                    symbol,
                    close_price,
                    unix_date,
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
                    co.company_name
                FROM growth g
                LEFT JOIN companies co ON co.symbol = g.symbol
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

            jobInfo.ProcessingMessage = "Computing RSI and Volatility";
            var symbolNames = symbols.Select(x => x.Symbol).ToArray();

            await using var rsiConnection = new NpgsqlConnection(_connectionString);
            await rsiConnection.OpenAsync(cancellationToken);

            var cpviTask = CpviCalculator.CalculateAsync(symbolNames, tableName, connection,
                startJobParameters.StartUnixDate * 1000, startJobParameters.EndUnixDate * 1000,
                isCedear ? null : startJobParameters.Exchange, cancellationToken);
            var rsiTask = RsiCalculator.CalculateAsync(symbolNames, tableName, rsiConnection, cancellationToken: cancellationToken);

            await Task.WhenAll(cpviTask, rsiTask);

            var cpviMap = (await cpviTask).ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var s in symbols)
                if (cpviMap.TryGetValue(s.Symbol, out var cpvi))
                    s.Volatility = cpvi;

            var rsiMap = (await rsiTask).ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbols)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            return symbols;
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
