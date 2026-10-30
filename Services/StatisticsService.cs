using growy_server.Calculators;
using growy_server.Models;
using Npgsql;

namespace growy_server.Services
{
    public class StatisticsService(IConfiguration configuration) : IStatisticsService
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        public async Task<SymbolHistoryResult> GetSymbolHistory(string symbol, GetSymbolHistoryParameters parameters, CancellationToken cancellationToken = default)
        {
            var (tableName, isCedear, _) = PriceTables.Resolve(parameters.Exchange);

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
    }
}
