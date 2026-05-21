using growy_server.Models;
using Npgsql;

namespace growy_server.Calculators
{
    public static class CpviCalculator
    {
        private const double NoMovementSentinel = 9999999;

        public static async Task<List<CPVIResult>> CalculateAsync(
            string[] symbols, string tableName, NpgsqlConnection connection,
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

            var results = new List<CPVIResult>();
            foreach (var group in rows.GroupBy(r => r.Symbol))
            {
                var prices = group.Select(r => r.ClosePrice).ToList();
                results.Add(ComputeCpvi(group.Key, prices));
            }

            return results.OrderByDescending(r => r.CPVI).ToList();
        }

        public static CPVIResult ComputeCpvi(string symbol, IReadOnlyList<double> closePricesOrderedByDate)
        {
            if (closePricesOrderedByDate.Count < 2)
                return new CPVIResult { Symbol = symbol, CPVI = NoMovementSentinel };

            double start = closePricesOrderedByDate[0];
            double end = closePricesOrderedByDate[closePricesOrderedByDate.Count - 1];
            double denominator = Math.Abs(start - end);

            if (denominator == 0)
                return new CPVIResult { Symbol = symbol, CPVI = NoMovementSentinel };

            double numerator = 0;
            for (int i = 1; i < closePricesOrderedByDate.Count; i++)
                numerator += Math.Abs(closePricesOrderedByDate[i] - closePricesOrderedByDate[i - 1]);

            return new CPVIResult { Symbol = symbol, CPVI = numerator / denominator };
        }
    }
}
