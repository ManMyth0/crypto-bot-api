using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace crypto_bot_api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        { }

        public DbSet<TradeRecords> TradeRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Explicit mapping
            modelBuilder.Entity<TradeRecords>().ToTable("TradeRecords", schema: "public");
        }
    }

    [Table("TradeRecords", Schema = "public")] // For clarity
    public class TradeRecords
    {
        [Key]
        public int TradeId { get; set; }
        public DateTime? TradeTime { get; set; }

        // Acquisition Details
        public decimal? AcquiredPrice { get; set; }
        public decimal? AcquiredQuantity { get; set; }

        // Offloading Details
        public decimal? SoldPrice { get; set; }
        public DateTime? OffloadTime { get; set; }

        // Trade status and profit/loss tracking
        public bool? IsSuccessful { get; set; }
        public decimal? ProfitLoss { get; set; }

        // Trade classification ( Buy / Sell ) 
        public string? TradeType { get; set; }

        // Percentage of return calculation ( stored in DB )
        public decimal? PercentageOfReturn { get; set; }
    }
}