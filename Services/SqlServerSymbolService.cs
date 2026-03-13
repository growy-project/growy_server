using growy_server.Data;
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
    }
}
