using growy_server.Models;

namespace growy_server.Services
{
    public interface IStatisticsService
    {
        Task<SymbolHistoryResult> GetSymbolHistory(string symbol, GetSymbolHistoryParameters parameters, CancellationToken cancellationToken = default);
    }
}
