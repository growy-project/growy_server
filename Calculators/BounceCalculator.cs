using growy_server.Models;
using Npgsql;

namespace growy_server.Calculators
{
    public static class BounceCalculator
    {
        public static async Task<List<BounceResult>> CalculateAsync(
            string[] symbols, string tableName, NpgsqlConnection connection,
            IReadOnlyDictionary<string, double> targetPrices,
            long startDate = 0, long endDate = long.MaxValue, string? exchange = null,
            CancellationToken cancellationToken = default)
        {
            if (symbols.Length == 0)
                return [];

            var paramPlaceholders = new List<string>();
            var parameters = new List<NpgsqlParameter>();

            for (int i = 0; i < symbols.Length; i++)
            {
                paramPlaceholders.Add($"@p{i}");
                parameters.Add(new NpgsqlParameter($"@p{i}", symbols[i]));
            }

            string inClause = string.Join(", ", paramPlaceholders);
            string dateFilter = startDate > 0 ? "AND unix_date BETWEEN @startDate AND @endDate" : "";
            string exchangeFilter = exchange != null ? "AND exchange = @exchange" : "";

            string sql = $@"
                SELECT symbol AS Symbol, close_price AS ClosePrice
                FROM {tableName}
                WHERE symbol IN ({inClause}) {dateFilter} {exchangeFilter}
                ORDER BY symbol, unix_date ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var p in parameters)
                command.Parameters.Add(p);
            if (startDate > 0)
            {
                command.Parameters.AddWithValue("@startDate", startDate);
                command.Parameters.AddWithValue("@endDate", endDate);
            }
            if (exchange != null)
                command.Parameters.AddWithValue("@exchange", exchange);

            var rows = new List<(string Symbol, double ClosePrice)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add((reader.GetString(0), reader.GetDouble(1)));

            var results = new List<BounceResult>();
            foreach (var group in rows.GroupBy(r => r.Symbol))
            {
                var prices = group.Select(r => r.ClosePrice).ToList();
                double targetPrice = targetPrices.TryGetValue(group.Key, out var t) ? t : 0;
                results.Add(new BounceResult { Symbol = group.Key, IsBouncing = ComputeIsBouncing(prices, targetPrice) });
            }

            return results;
        }

        public static bool ComputeIsBouncing(IReadOnlyList<double> closesOrderedByDate, double targetPrice)
        {
            int n = closesOrderedByDate.Count;
            if (n < 2)
                return false;

            double latest = closesOrderedByDate[n - 1];

            // Analyst upside: target must sit above today's close (also excludes target == 0, i.e. no analyst data).
            if (targetPrice <= latest)
                return false;

            // Peak = window high; take its first occurrence for the longest post-peak window.
            int peakIndex = 0;
            double peak = closesOrderedByDate[0];
            for (int i = 1; i < n; i++)
                if (closesOrderedByDate[i] > peak)
                {
                    peak = closesOrderedByDate[i];
                    peakIndex = i;
                }

            // Must be below a prior high, and that high must be in the past (bars remain after it).
            if (peak <= latest || peakIndex >= n - 1)
                return false;

            double postPeakLow = double.MaxValue;
            for (int i = peakIndex + 1; i < n; i++)
                if (closesOrderedByDate[i] < postPeakLow)
                    postPeakLow = closesOrderedByDate[i];

            // Recovering: today's close is above the post-peak low.
            return latest > postPeakLow;
        }
    }
}
