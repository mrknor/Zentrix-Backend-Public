using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Data
{
    public class StockDbContext : DbContext
    {
        public DbSet<Stock> Stocks { get; set; }
        public DbSet<Watchlist> Watchlists { get; set; } // New Watchlist DbSet

        public DbSet<SentimentSummary> SentimentSummaries { get; set; }

        // Data/StockDbContext.cs
        public DbSet<PaperTrade> PaperTrades { get; set; }

        public DbSet<PaperTradingAccount> PaperTradingAccounts { get; set; }

        public StockDbContext(DbContextOptions<StockDbContext> options) : base(options)

        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Backend.Models.Stock>().HasKey(s => s.Ticker);
            base.OnModelCreating(modelBuilder);
        }
    }
}

