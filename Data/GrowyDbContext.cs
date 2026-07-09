using growy_server.Models.DB;
using Microsoft.EntityFrameworkCore;

namespace growy_server.Data
{
    public class GrowyDbContext : DbContext
    {
        public GrowyDbContext(DbContextOptions<GrowyDbContext> options) : base(options) { }

        public DbSet<SymbolDatePrice> SymbolDatePrices { get; set; }
        public DbSet<SymbolDatePriceCedear> SymbolDatePriceCedears { get; set; }
        public DbSet<UserEntity> Users { get; set; }
        public DbSet<CompanyEntity> Companies { get; set; }
        public DbSet<UserWatchlistEntity> UserWatchlist { get; set; }
        public DbSet<EmailMessageEntity> EmailMessages { get; set; }
    }
}
