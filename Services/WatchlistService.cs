using System.Data;
using growy_server.Data;
using growy_server.Models;
using growy_server.Models.DB;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace growy_server.Services
{
    public class WatchlistService(GrowyDbContext db) : IWatchlistService
    {
        public const int MaxSymbolsPerUser = 50;
        public const string UnknownSector = "Unknown";
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

        public async Task<List<WatchlistSectorResult>> GetSectorDistributionAsync(int userId, CancellationToken cancellationToken = default)
        {
            // Joined on symbol alone: companies.symbol is the PK and holds the underlying ticker,
            // so a CEDEAR watchlist row still resolves (companies.exchange would be its NYSE/NASDAQ
            // listing, and matching on it too would drop every CEDEAR). LEFT JOIN because nothing
            // guarantees a companies row exists for a saved symbol.
            var rows = await (from w in db.UserWatchlist
                              where w.UserId == userId
                              join c in db.Companies on w.Symbol equals c.Symbol into companies
                              from c in companies.DefaultIfEmpty()
                              select new { w.Symbol, w.Exchange, c.Sector })
                             .ToListAsync(cancellationToken);

            return BuildSectorDistribution(
                rows.Select(r => new WatchlistSectorRow(r.Symbol, r.Exchange, r.Sector)).ToList());
        }

        public static List<WatchlistSectorResult> BuildSectorDistribution(IReadOnlyList<WatchlistSectorRow> rows)
        {
            if (rows.Count == 0)
                return [];

            return rows
                .GroupBy(r => string.IsNullOrWhiteSpace(r.Sector) ? UnknownSector : r.Sector)
                .Select(g => new WatchlistSectorResult
                {
                    Sector = g.Key,
                    Count = g.Count(),
                    Percentage = Math.Round(g.Count() * 100d / rows.Count, 2),
                    Symbols = g
                        .OrderBy(r => r.Symbol, StringComparer.Ordinal)
                        .ThenBy(r => r.Exchange, StringComparer.Ordinal)
                        .Select(r => new WatchlistSectorSymbol { Symbol = r.Symbol, Exchange = r.Exchange })
                        .ToList()
                })
                .OrderBy(s => s.Sector == UnknownSector)
                .ThenByDescending(s => s.Count)
                .ThenBy(s => s.Sector, StringComparer.Ordinal)
                .ToList();
        }
    }

    public class WatchlistSectorRow(string symbol, string exchange, string? sector)
    {
        public string Symbol { get; } = symbol;
        public string Exchange { get; } = exchange;
        public string? Sector { get; } = sector;
    }
}
