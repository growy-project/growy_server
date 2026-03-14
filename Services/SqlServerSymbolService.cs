using growy_server.Data;
using growy_server.Models;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Services
{
    public class SqlServerSymbolService(IServiceScopeFactory scopeFactory) : ISymbolService
    {
        public async Task SetSymbolAsTopGrowth(string symbol, bool value)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GrowyDbContext>();

            var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol)
                ?? throw new KeyNotFoundException($"Symbol '{symbol}' not found in companies.");

            company.IsTopGrowth = value;
            await db.SaveChangesAsync();
        }

        public async Task SetSymbolAsToxic(string symbol, bool value)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GrowyDbContext>();

            var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol)
                ?? throw new KeyNotFoundException($"Symbol '{symbol}' not found in companies.");

            company.IsToxic = value;
            await db.SaveChangesAsync();
        }

        public async Task<SymbolDateRangeResult> GetDateRange(string exchange)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GrowyDbContext>();

            if (exchange.ToUpper() == "CEDEAR")
            {
                var firstDate = await db.SymbolDatePriceCedears.MinAsync(p => p.UnixDate);
                var lastDate  = await db.SymbolDatePriceCedears.MaxAsync(p => p.UnixDate);
                return new SymbolDateRangeResult { FirstDate = firstDate, LastDate = lastDate };
            }
            else
            {
                var query = db.SymbolDatePrices.Where(p => p.Exchange == exchange);
                var firstDate = await query.MinAsync(p => p.UnixDate);
                var lastDate  = await query.MaxAsync(p => p.UnixDate);
                return new SymbolDateRangeResult { FirstDate = firstDate, LastDate = lastDate };
            }
        }
    }
}
