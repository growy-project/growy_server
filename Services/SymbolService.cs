using growy_server.Data;
using growy_server.Models;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Services
{
    public class SymbolService(GrowyDbContext db) : ISymbolService
    {
        public async Task SetSymbolAsTopGrowth(string symbol, bool value, CancellationToken cancellationToken = default)
        {
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol, cancellationToken)
                ?? throw new KeyNotFoundException($"Symbol '{symbol}' not found.");

            company.IsTopGrowth = value;
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetSymbolAsToxic(string symbol, bool value, CancellationToken cancellationToken = default)
        {
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol, cancellationToken)
                ?? throw new KeyNotFoundException($"Symbol '{symbol}' not found.");

            company.IsToxic = value;
            await db.SaveChangesAsync(cancellationToken);
        }

        public async Task<SymbolDateRangeResult> GetDateRange(string exchange, CancellationToken cancellationToken = default)
        {
            if (exchange.ToUpper() == "CEDEAR")
            {
                var firstDate = await db.SymbolDatePriceCedears.MinAsync(p => p.UnixDate, cancellationToken);
                var lastDate  = await db.SymbolDatePriceCedears.MaxAsync(p => p.UnixDate, cancellationToken);
                return new SymbolDateRangeResult { FirstDate = firstDate, LastDate = lastDate };
            }
            else
            {
                var query = db.SymbolDatePrices.Where(p => p.Exchange == exchange);
                var firstDate = await query.MinAsync(p => p.UnixDate, cancellationToken);
                var lastDate  = await query.MaxAsync(p => p.UnixDate, cancellationToken);
                return new SymbolDateRangeResult { FirstDate = firstDate, LastDate = lastDate };
            }
        }
    }
}
