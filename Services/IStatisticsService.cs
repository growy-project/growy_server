using growy_server.Models;

namespace growy_server.Services
{
    public interface IStatisticsService
    {
        Task<List<SymbolResult>> GetTopGrowth(StartStatisticJobParameters StartJobParameters, StatisticJobInfo jobInfo, CancellationToken cancellationToken = default);
        Task<SymbolHistoryResult> GetSymbolHistory(string symbol, string exchange, CancellationToken cancellationToken = default);
    }
}
