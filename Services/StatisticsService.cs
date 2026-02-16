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
                          WHERE unix_date BETWEEN @StartDate AND @EndDate
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

            // asignamos parámetros
            command.Parameters.AddWithValue("@StartDate", startJobParameters.StartUnixDate * 1000);
            command.Parameters.AddWithValue("@EndDate", startJobParameters.EndUnixDate * 1000);
            command.Parameters.AddWithValue("@Threshold", startJobParameters.MinimumExpectedGrowth);

            await using var reader = await command.ExecuteReaderAsync();


            while (await reader.ReadAsync())
            {
                var symbolResult = new SymbolResult
                {
                    Symbol = reader.GetString(0),
                    PercentageChange = reader.GetDouble(1), // Or GetDecimal if it's a decimal type in the database
                    OldestPrice = reader.GetDouble(2), // Or GetDecimal
                    NewestPrice = reader.GetDouble(3),  // Or GetDecimal
                    MarketCap = 0,
                    EarningsPerShare = 0,
                    TargetPrice = 0,
                    Rsi = 0,
                    Volatility = 0,
                };
                symbols.Add(symbolResult);
            }

            // var volatilityResult = CalculateCPVIs(symbols.Select(x => x.Symbol).ToArray());

            return symbols;
        }

        //calculate Close Price Variation Index
        public List<CPVIResult> CalculateCPVIs(string[] symbols)
        {
            var cpviResults = new List<CPVIResult>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                connection.Open();

                foreach (var symbol in symbols)
                {
                    using (var command = new NpgsqlCommand(@"
                    SELECT symbol, close_price, unix_date
                    FROM symbol_date_price
                    WHERE symbol = @symbol
                    ORDER BY unix_date ASC", connection)) // Order by unix_date!
                    {
                        command.Parameters.AddWithValue("@symbol", symbol);

                        using (var reader = command.ExecuteReader())
                        {
                            var symbolDataList = new List<SymbolData>();
                            while (reader.Read())
                            {
                                symbolDataList.Add(new SymbolData
                                {
                                    symbol = reader.GetString(0),
                                    close_price = reader.GetDouble(1),
                                    unix_date = reader.GetInt64(2)
                                });
                            }

                            if (symbolDataList.Count > 0) // Check if data was found for the symbol
                            {
                                var prices = symbolDataList.Select(r => r.close_price).ToArray();
                                var startPrice = prices[0];
                                var endPrice = prices[^1];

                                double sumAbsDiff = 0;
                                for (int i = 1; i < prices.Length; i++)
                                {
                                    sumAbsDiff += Math.Abs(prices[i] - prices[i - 1]);
                                }

                                double cpvi = Math.Abs(startPrice - endPrice) > 0 ? sumAbsDiff / Math.Abs(startPrice - endPrice) : double.PositiveInfinity;
                                cpviResults.Add(new CPVIResult { Symbol = symbol, CPVI = cpvi });
                            }
                            else
                            {
                                //Handle the case where no data is found for the symbol
                                Console.WriteLine($"No data found for symbol: {symbol}");
                            }


                        }
                    }
                }
            }

            return cpviResults.OrderByDescending(r => r.CPVI).ToList();
        }
    }
}
