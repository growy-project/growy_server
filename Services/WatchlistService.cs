using System.Data;
using growy_server.Data;
using growy_server.Models.DB;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace growy_server.Services
{
    public class WatchlistService(GrowyDbContext db) : IWatchlistService
    {
        public const int MaxSymbolsPerUser = 20;
        private const string PostgresUniqueViolation = "23505";

        public async Task AddAsync(int userId, string symbol, string exchange, CancellationToken cancellationToken = default)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            var count = await db.UserWatchlist.CountAsync(w => w.UserId == userId, cancellationToken);
            if (count >= MaxSymbolsPerUser)
                throw new WatchlistLimitReachedException(MaxSymbolsPerUser);

            db.UserWatchlist.Add(new UserWatchlistEntity
            {
                UserId = userId,
                Symbol = symbol,
                Exchange = exchange,
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg && pg.SqlState == PostgresUniqueViolation)
            {
                await transaction.RollbackAsync(cancellationToken);
                throw new WatchlistDuplicateException(symbol, exchange);
            }
        }

        public async Task<bool> RemoveAsync(int userId, string symbol, string exchange, CancellationToken cancellationToken = default)
        {
            var entity = await db.UserWatchlist
                .FirstOrDefaultAsync(w => w.UserId == userId && w.Symbol == symbol && w.Exchange == exchange, cancellationToken);

            if (entity is null)
                return false;

            db.UserWatchlist.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        public Task<List<UserWatchlistEntity>> GetSymbolsAsync(int userId, CancellationToken cancellationToken = default)
        {
            return db.UserWatchlist
                .Where(w => w.UserId == userId)
                .OrderBy(w => w.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
