using growy_server.Models.DB;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Data
{
    public class GrowyDbContext : DbContext
    {
        public GrowyDbContext(DbContextOptions<GrowyDbContext> options) : base(options) { }

        public DbSet<SymbolDatePrice> SymbolDatePrices { get; set; }
        public DbSet<SymbolDatePriceCedear> SymbolDatePriceCedears { get; set; }
    }
}
