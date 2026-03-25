using growy_server.Data;
using growy_server.Models;
using growy_server.Models.DB;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Services
{
    public class SymbolService(GrowyDbContext db) : ISymbolService
    {
        public Task SetSymbolAsTopGrowth(string symbol, bool value, CancellationToken cancellationToken = default)
            => UpdateCompanyAsync(symbol, company => company.IsTopGrowth = value, cancellationToken);

        public Task SetSymbolAsToxic(string symbol, bool value, CancellationToken cancellationToken = default)
            => UpdateCompanyAsync(symbol, company => company.IsToxic = value, cancellationToken);

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

        private async Task UpdateCompanyAsync(string symbol, Action<CompanyEntity> update, CancellationToken cancellationToken)
        {
            var company = await db.Companies.FirstOrDefaultAsync(c => c.Symbol == symbol, cancellationToken)
                ?? throw new KeyNotFoundException($"Symbol '{symbol}' not found.");

            update(company);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
