using growy_server.Models;
using Npgsql;

namespace growy_server.Services
{
    public class StatisticsService(IConfiguration configuration) : IStatisticsService
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        public async Task<List<SymbolResult>> GetTopGrowth(StartStatisticJobParameters startJobParameters, StatisticJobInfo jobInfo)
        {
            var symbols = new List<SymbolResult>();

            string tableName = startJobParameters.Exchange != "CEDEAR" ? "symbol_date_price" : "symbol_date_price_cedears";
            bool isCedear = startJobParameters.Exchange == "CEDEAR";
            string exchangeFilter = isCedear ? "" : "AND exchange = @Exchange";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

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
                )
                SELECT *
                FROM crecimientos
                WHERE percentageChange > @Threshold
                ORDER BY percentageChange DESC;";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@StartDate", startJobParameters.StartUnixDate * 1000);
            command.Parameters.AddWithValue("@EndDate", startJobParameters.EndUnixDate * 1000);
            command.Parameters.AddWithValue("@Threshold", startJobParameters.MinimumExpectedGrowth);

            if (!isCedear)
                command.Parameters.AddWithValue("@Exchange", startJobParameters.Exchange);

            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    symbols.Add(new SymbolResult
                    {
                        Symbol = reader.GetString(0),
                        PercentageChange = reader.GetDouble(1),
                        OldestPrice = reader.GetDouble(2),
                        NewestPrice = reader.GetDouble(3),
                        MarketCap = 0,
                        EarningsPerShare = 0,
                        TargetPrice = 0,
                        Rsi = 0,
                        Volatility = 0,
                    });
                }
            } // reader closed before reusing the connection

            jobInfo.ProcessingMessage = "Computing volatility";
            var cpviResults = CalculateCPVIs(symbols.Select(x => x.Symbol).ToArray(), tableName, connection);
            var cpviMap = cpviResults.ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var s in symbols)
                if (cpviMap.TryGetValue(s.Symbol, out var cpvi))
                    s.Volatility = cpvi;

            jobInfo.ProcessingMessage = "Computing RSI";
            var rsiResults = await CalculateRSIs(symbols.Select(x => x.Symbol).ToArray(), tableName, connection);
            var rsiMap = rsiResults.ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var s in symbols)
                if (rsiMap.TryGetValue(s.Symbol, out var rsi))
                    s.Rsi = rsi;

            return symbols;
        }

        public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, string exchange)
        {
            string tableName = exchange != "CEDEAR" ? "symbol_date_price" : "symbol_date_price_cedears";
            bool isCedear = exchange == "CEDEAR";

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

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
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                prices.Add(new PriceEntry
                {
                    ClosePrice = reader.GetDouble(0),
                    UnixDate = reader.GetInt64(1)
                });
            }

            return new SymbolHistoryResult { Symbol = symbol, Prices = prices, Ema20 = Calculate20Ema(prices) };
        }

        private static List<EmaEntry> Calculate20Ema(List<PriceEntry> prices)
        {
            const int period = 20;
            var emaEntries = new List<EmaEntry>();

            if (prices.Count < period)
                return emaEntries;

            double k = 2.0 / (period + 1);

            double ema = prices.Take(period).Average(p => p.ClosePrice);
            emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[period - 1].UnixDate });

            for (int i = period; i < prices.Count; i++)
            {
                ema = prices[i].ClosePrice * k + ema * (1 - k);
                emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[i].UnixDate });
            }

            return emaEntries;
        }

        public List<CPVIResult> CalculateCPVIs(string[] symbols, string tableName, NpgsqlConnection connection)
        {
            if (symbols.Length == 0)
                return new List<CPVIResult>();

            var paramPlaceholders = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                parameters.Add(new NpgsqlParameter($"@p{i}", symbols[i]));
            }

            string inClause = string.Join(", ", paramPlaceholders);

            string sql = $@"
                SELECT
                    symbol AS Symbol,
                    CASE
                        WHEN ABS(start_price - end_price) = 0 THEN 9999999
                        ELSE SUM(ABS(close_price - prev_price)) / ABS(start_price - end_price)
                    END AS CPVI
                FROM (
                    SELECT
                        symbol,
                        close_price,
                        LAG(close_price) OVER (PARTITION BY symbol ORDER BY unix_date) AS prev_price,
                        FIRST_VALUE(close_price) OVER (PARTITION BY symbol ORDER BY unix_date
                            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS start_price,
                        FIRST_VALUE(close_price) OVER (PARTITION BY symbol ORDER BY unix_date DESC
                            ROWS BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING) AS end_price
                    FROM {tableName}
                    WHERE symbol IN ({inClause})
                ) sub
                WHERE prev_price IS NOT NULL
                GROUP BY symbol, start_price, end_price
                ORDER BY CPVI DESC";

            using var command = new NpgsqlCommand(sql, connection);
            foreach (var p in parameters)
                command.Parameters.Add(p);

            var results = new List<CPVIResult>();
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new CPVIResult
                {
                    Symbol = reader.GetString(0),
                    CPVI = reader.GetDouble(1)
                });
            }

            return results;
        }

        private async Task<List<RsiResult>> CalculateRSIs(string[] symbols, string tableName, NpgsqlConnection connection, int period = 14)
        {
            if (symbols.Length == 0)
                return new List<RsiResult>();

            var paramPlaceholders = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                parameters.Add(new NpgsqlParameter($"@p{i}", symbols[i]));
            }

            string inClause = string.Join(", ", paramPlaceholders);

            string sql = $@"
                SELECT symbol AS Symbol, close_price AS ClosePrice
                FROM {tableName}
                WHERE symbol IN ({inClause})
                ORDER BY symbol, unix_date ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var p in parameters)
                command.Parameters.Add(p);

            var rows = new List<(string Symbol, double ClosePrice)>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                rows.Add((reader.GetString(0), reader.GetDouble(1)));

            var results = new List<RsiResult>();

            foreach (var group in rows.GroupBy(r => r.Symbol))
            {
                var prices = group.Select(r => r.ClosePrice).ToList();

                if (prices.Count < period + 1)
                    continue;

                var gains = new double[prices.Count - 1];
                var losses = new double[prices.Count - 1];

                for (int i = 0; i < prices.Count - 1; i++)
                {
                    double change = prices[i + 1] - prices[i];
                    gains[i] = change > 0 ? change : 0;
                    losses[i] = change < 0 ? -change : 0;
                }

                double avgGain = 0;
                double avgLoss = 0;

                for (int i = 0; i < period; i++)
                {
                    avgGain += gains[i];
                    avgLoss += losses[i];
                }
                avgGain /= period;
                avgLoss /= period;

                for (int i = period; i < gains.Length; i++)
                {
                    avgGain = (avgGain * (period - 1) + gains[i]) / period;
                    avgLoss = (avgLoss * (period - 1) + losses[i]) / period;
                }

                double rsi = avgLoss == 0 ? 100 : 100 - (100 / (1 + avgGain / avgLoss));
                results.Add(new RsiResult { Symbol = group.Key, Rsi = Math.Round(rsi, 2) });
            }

            return results;
        }
    }
}
