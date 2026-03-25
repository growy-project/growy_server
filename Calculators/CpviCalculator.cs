using growy_server.Models;
using Npgsql;

namespace growy_server.Calculators
{
    public static class CpviCalculator
    {
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
                    WHERE symbol IN ({inClause}) {dateFilter} {exchangeFilter}
                ) sub
                WHERE prev_price IS NOT NULL
                GROUP BY symbol, start_price, end_price
                ORDER BY CPVI DESC";

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

            var results = new List<CPVIResult>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new CPVIResult
                {
                    Symbol = reader.GetString(0),
                    CPVI = reader.GetDouble(1)
                });
            }

            return results;
        }
    }
}
