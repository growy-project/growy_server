using growy_server.Models;

namespace growy_server.Services
{
    public interface ISymbolService
    {
        Task SetSymbolAsTopGrowth(string symbol, bool value);
        Task SetSymbolAsToxic(string symbol, bool value);
        Task<SymbolDateRangeResult> GetDateRange(string exchange);
    }
}
