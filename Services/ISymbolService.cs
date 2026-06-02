using growy_server.Models;

namespace growy_server.Services
{
    public interface ISymbolService
    {
        Task SetSymbolAsTopGrowth(string symbol, bool value, CancellationToken cancellationToken = default);
        Task<SymbolDateRangeResult> GetDateRange(string exchange, CancellationToken cancellationToken = default);
    }
}
