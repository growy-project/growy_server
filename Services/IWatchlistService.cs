using growy_server.Models.DB;

namespace growy_server.Services
{
    public interface IWatchlistService
    {
        Task AddAsync(int userId, string symbol, string exchange, CancellationToken cancellationToken = default);
        Task<bool> RemoveAsync(int userId, string symbol, string exchange, CancellationToken cancellationToken = default);
        Task<List<UserWatchlistEntity>> GetSymbolsAsync(int userId, CancellationToken cancellationToken = default);
    }

    public class WatchlistLimitReachedException : Exception
    {
        public WatchlistLimitReachedException(int limit)
            : base($"Watchlist limit reached ({limit} symbols)") { }
    }

    public class WatchlistDuplicateException : Exception
    {
        public WatchlistDuplicateException(string symbol, string exchange)
            : base($"Symbol {symbol} ({exchange}) is already in your watchlist") { }
    }
}
