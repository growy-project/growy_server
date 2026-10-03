using Npgsql;

namespace growy_server.Calculators
{
    public static class PriceSeriesLoader
    {
        // Shared windowed price load for calculators that need the identical rows,
        // consumed via CalculateFromSeries.
        // Returns symbol -> close prices ordered oldest-first.
        public static async Task<Dictionary<string, List<double>>> LoadAsync(
            string[] symbols, string tableName, NpgsqlConnection connection,
            long startDate = 0, long endDate = long.MaxValue, string? exchange = null,
            CancellationToken cancellationToken = default)
        {
            var series = new Dictionary<string, List<double>>();

            if (symbols.Length == 0)
                return series;

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

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                string symbol = reader.GetString(0);
                if (!series.TryGetValue(symbol, out var prices))
                {
                    prices = [];
                    series[symbol] = prices;
                }
                prices.Add(reader.GetDouble(1));
            }

            return series;
        }
    }
}
