using growy_server.Models;
using Npgsql;

namespace growy_server.Calculators
{
    public static class RsiCalculator
    {
        public static async Task<List<RsiResult>> CalculateAsync(
            string[] symbols, string tableName, NpgsqlConnection connection,
            int period = 14, CancellationToken cancellationToken = default)
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

            string sql = $@"
                SELECT symbol AS Symbol, close_price AS ClosePrice
                FROM {tableName}
                WHERE symbol IN ({inClause})
                ORDER BY symbol, unix_date ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var p in parameters)
                command.Parameters.Add(p);

            var rows = new List<(string Symbol, double ClosePrice)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
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
