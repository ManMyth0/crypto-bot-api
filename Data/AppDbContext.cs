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
            
            // Index for faster position lookups
            modelBuilder.Entity<TradeRecords>()
                .HasIndex(t => new { t.asset_pair, t.is_position_closed, t.leftover_quantity })
                .HasFilter("leftover_quantity > 0");
        }
    }

    [Table("TradeRecords", Schema = "public")]
    public class TradeRecords
    {
        [Key]
        [Column("position_uuid")]
        public Guid position_uuid { get; set; }

        [Column("asset_pair")]
        [StringLength(20)]
        public string? asset_pair { get; set; }

        [Column("acquired_price")]
        [Precision(18, 8)]
        public decimal? acquired_price { get; set; }

        [Column("acquired_quantity")]
        [Precision(18, 8)]
        public decimal? acquired_quantity { get; set; }

        [Column("leftover_quantity")]
        [Precision(18, 8)]
        public decimal? leftover_quantity { get; set; }

        [Column("profit_loss")]
        [Precision(18, 8)]
        public decimal? profit_loss { get; set; }

        [Column("percentage_return")]
        [Precision(18, 8)]
        public decimal? percentage_return { get; set; }

        [Column("total_commissions")]
        [Precision(18, 8)]
        public decimal? total_commissions { get; set; }

        [Column("is_position_closed")]
        public bool is_position_closed { get; set; } = false;

        [Column("last_updated")]
        public DateTime last_updated { get; set; } = DateTime.UtcNow;
    }
}