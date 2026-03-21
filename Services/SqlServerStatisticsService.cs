using growy_server.Data;
using growy_server.Models;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Services
{
    public class SqlServerStatisticsService(GrowyDbContext db) : IStatisticsService
    {
        public async Task<List<SymbolResult>> GetTopGrowth(StartStatisticJobParameters startJobParameters, StatisticJobInfo jobInfo)
        {
            jobInfo.ProcessingMessage = $"Retrieving stocks with expected growth > {startJobParameters.MinimumExpectedGrowth}%";

            long startDate = startJobParameters.StartUnixDate * 1000;
            long endDate = startJobParameters.EndUnixDate * 1000;
            string exchange = startJobParameters.Exchange;

            string tableName = exchange != "CEDEAR" ? "symbol_date_price" : "symbol_date_price_cedears";
            bool isCedear = exchange == "CEDEAR";
            string whereFilter  = isCedear ? "" : "AND exchange = @exchange";
            string joinFilterO  = isCedear ? "" : "AND o.exchange = @exchange";
            string joinFilterN  = isCedear ? "" : "AND n.exchange = @exchange";

            string sql = $@"
                WITH bounds AS (
                    SELECT symbol, MIN(unix_date) AS min_date, MAX(unix_date) AS max_date
                    FROM {tableName}
                    WHERE unix_date >= @startDate AND unix_date <= @endDate {whereFilter}
                    GROUP BY symbol
                )
                SELECT
                    b.symbol      AS Symbol,
                    o.close_price AS OldestPrice,
                    n.close_price AS NewestPrice
                FROM bounds b
                INNER JOIN {tableName} o
                    ON o.symbol = b.symbol AND o.unix_date = b.min_date {joinFilterO}
                INNER JOIN {tableName} n
                    ON n.symbol = b.symbol AND n.unix_date = b.max_date {joinFilterN}";

            var parameters = new List<object>
            {
                new Microsoft.Data.SqlClient.SqlParameter("@startDate", startDate),
                new Microsoft.Data.SqlClient.SqlParameter("@endDate", endDate)
            };

            if (exchange != "CEDEAR")
                parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@exchange", exchange));

            var grouped = await db.Database
                .SqlQueryRaw<SymbolGrowthRaw>(sql, parameters.ToArray())
                .ToListAsync();

            var results = grouped
                .Where(g => g.OldestPrice != 0)
                .Select(g => new SymbolResult
                {
                    Symbol = g.Symbol,
                    PercentageChange = ((g.NewestPrice - g.OldestPrice) / g.OldestPrice) * 100,
                    OldestPrice = g.OldestPrice,
                    NewestPrice = g.NewestPrice,
                    TargetPrice = 0,
                    Rsi = 0,
                    Volatility = 0
                })
                .Where(r => r.PercentageChange > startJobParameters.MinimumExpectedGrowth)
                .OrderByDescending(r => r.PercentageChange)
                .ToList();

            var symbols = results.Select(r => r.Symbol).ToList();
            var companies = await db.Companies
                .Where(c => symbols.Contains(c.Symbol))
                .ToListAsync();
            var companyMap = companies.ToDictionary(c => c.Symbol);

            foreach (var result in results)
            {
                if (companyMap.TryGetValue(result.Symbol, out var company))
                {
                    result.CompanyName = company.CompanyName;
                    result.Description = company.Description;
                    result.Sector = company.Sector;
                    result.MarketCapitalization = company.MarketCapitalization;
                    result.Eps = company.Eps;
                    result.TargetPrice = company.AnalystTargetPrice.HasValue
                        ? (double)company.AnalystTargetPrice.Value
                        : 0;
                }
            }

            jobInfo.ProcessingMessage = "Computing volatility";

            var cpviResults = await CalculateCPVIs(db, results.Select(r => r.Symbol).ToArray(), tableName);

            var cpviMap = cpviResults.ToDictionary(c => c.Symbol, c => c.CPVI);
            foreach (var result in results)
            {
                if (cpviMap.TryGetValue(result.Symbol, out var cpvi))
                {
                    result.Volatility = cpvi;
                }
            }

            jobInfo.ProcessingMessage = "Computing RSI";

            var rsiResults = await CalculateRSIs(db, results.Select(r => r.Symbol).ToArray(), tableName);

            var rsiMap = rsiResults.ToDictionary(r => r.Symbol, r => r.Rsi);
            foreach (var result in results)
            {
                if (rsiMap.TryGetValue(result.Symbol, out var rsi))
                {
                    result.Rsi = rsi;
                }
            }

            return results;
        }

        public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, string exchange)
        {
            List<PriceEntry> prices;

            if (exchange == "CEDEAR")
            {
                prices = await db.SymbolDatePriceCedears
                    .Where(p => p.Symbol == symbol)
                    .OrderBy(p => p.UnixDate)
                    .Select(p => new PriceEntry { ClosePrice = p.ClosePrice, UnixDate = p.UnixDate })
                    .ToListAsync();
            }
            else
            {
                prices = await db.SymbolDatePrices
                    .Where(p => p.Symbol == symbol && p.Exchange == exchange)
                    .OrderBy(p => p.UnixDate)
                    .Select(p => new PriceEntry { ClosePrice = p.ClosePrice, UnixDate = p.UnixDate })
                    .ToListAsync();
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

            // Seed: SMA(Simple Moving Average) of the first 20 closing prices
            double ema = prices.Take(period).Average(p => p.ClosePrice);
            emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[period - 1].UnixDate });

            // EMAₜ = Priceₜ × k + EMAₜ₋₁ × (1 − k)
            for (int i = period; i < prices.Count; i++)
            {
                ema = prices[i].ClosePrice * k + ema * (1 - k);
                emaEntries.Add(new EmaEntry { Value = ema, UnixDate = prices[i].UnixDate });
            }

            return emaEntries;
        }

        public async Task<List<CPVIResult>> CalculateCPVIs(GrowyDbContext db, string[] symbols, string tableName)
        {
            if (symbols.Length == 0)
                return new List<CPVIResult>();

            var parameters = new List<object>();
            var paramPlaceholders = new List<string>();

            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                parameters.Add(new Microsoft.Data.SqlClient.SqlParameter($"@p{i}", symbols[i]));
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

            var cpviResults = await db.Database
                .SqlQueryRaw<CPVIResult>(sql, parameters.ToArray())
                .ToListAsync();

            return cpviResults;
        }

        private async Task<List<RsiResult>> CalculateRSIs(GrowyDbContext db, string[] symbols, string tableName, int period = 14)
        {
            if (symbols.Length == 0)
                return new List<RsiResult>();

            var parameters = new List<object>();
            var paramPlaceholders = new List<string>();

            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                parameters.Add(new Microsoft.Data.SqlClient.SqlParameter($"@p{i}", symbols[i]));
            }

            string inClause = string.Join(", ", paramPlaceholders);

            string sql = $@"
                SELECT symbol AS Symbol, close_price AS ClosePrice
                FROM {tableName}
                WHERE symbol IN ({inClause})
                ORDER BY symbol, unix_date ASC";

            var rows = await db.Database
                .SqlQueryRaw<RsiRow>(sql, parameters.ToArray())
                .ToListAsync();

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

                double rsi;
                if (avgLoss == 0)
                    rsi = 100;
                else
                {
                    double rs = avgGain / avgLoss;
                    rsi = 100 - (100 / (1 + rs));
                }

                results.Add(new RsiResult { Symbol = group.Key, Rsi = Math.Round(rsi, 2) });
            }

            return results;
        }

        private class RsiRow
        {
            public string Symbol { get; set; }
            public double ClosePrice { get; set; }
        }

        private class SymbolGrowthRaw
        {
            public string Symbol { get; set; }
            public double OldestPrice { get; set; }
            public double NewestPrice { get; set; }
        }
    }
}
