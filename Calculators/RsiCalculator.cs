using growy_server.Models;
using Npgsql;
using NpgsqlTypes;

namespace growy_server.Calculators
{
    public static class RsiCalculator
    {
        // Bars of warm-up to load per period. Wilder smoothing is recursive, so the seed
        // never fully disappears — but its influence decays geometrically: after k*period
        // bars it is ((period-1)/period)^(k*period), which tends to e^-k for any period.
        // At k = 22 that is ~3e-10, far below the 2 dp the API serialises, so a bounded
        // tail is indistinguishable from full history while loading far fewer rows.
        private const int WarmupPeriods = 23;

        public static async Task<List<RsiResult>> CalculateAsync(
            string[] symbols, string tableName, NpgsqlConnection connection,
            int period = 14, string? exchange = null, CancellationToken cancellationToken = default)
        {
            if (symbols.Length == 0)
                return [];

            string exchangeFilter = exchange != null ? "AND t.exchange = @exchange" : "";

            // RSI here is a *current* overbought/oversold reading: the series always ends at
            // the newest bar, independent of the caller's analysis window. So only the most
            // recent WarmupPeriods*period bars per symbol are needed. A LATERAL per-symbol
            // LIMIT is used rather than a shared date cut-off because it stays exact for
            // thinly-traded and dormant tickers: any symbol holding fewer rows than the limit
            // simply loads all of them, exactly as the previous full-history load did.
            string sql = $@"
                SELECT s.symbol AS Symbol, p.close_price AS ClosePrice
                FROM unnest(@symbols) AS s(symbol)
                CROSS JOIN LATERAL (
                    SELECT t.close_price, t.unix_date
                    FROM {tableName} t
                    WHERE t.symbol = s.symbol {exchangeFilter}
                    ORDER BY t.unix_date DESC
                    LIMIT @warmupBars
                ) p
                ORDER BY s.symbol, p.unix_date ASC";

            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add(new NpgsqlParameter("symbols", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = symbols });
            command.Parameters.AddWithValue("@warmupBars", period * WarmupPeriods);
            if (exchange != null)
                command.Parameters.AddWithValue("@exchange", exchange);

            var rows = new List<(string Symbol, double ClosePrice)>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows.Add((reader.GetString(0), reader.GetDouble(1)));

            var results = new List<RsiResult>();

            foreach (var group in rows.GroupBy(r => r.Symbol))
            {
                var prices = group.Select(r => r.ClosePrice).ToList();
                var result = ComputeRsi(group.Key, prices, period);
                if (result != null)
                    results.Add(result);
            }

            return results;
        }

        public static RsiResult? ComputeRsi(string symbol, IReadOnlyList<double> closePricesOrderedByDate, int period = 14)
        {
            if (closePricesOrderedByDate.Count < period + 1)
                return null;

            int count = closePricesOrderedByDate.Count;
            var gains = new double[count - 1];
            var losses = new double[count - 1];

            for (int i = 0; i < count - 1; i++)
            {
                double change = closePricesOrderedByDate[i + 1] - closePricesOrderedByDate[i];
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
            return new RsiResult { Symbol = symbol, Rsi = Math.Round(rsi, 2) };
        }
    }
}
