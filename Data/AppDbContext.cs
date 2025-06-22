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
        public DbSet<OpeningTrades> OpeningTrades { get; set; }
        public DbSet<ClosingTrades> ClosingTrades { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TradeRecords>(entity =>
            {
                entity.ToTable("Trade_Records", "public");
                entity.HasKey(e => e.position_uuid);
                
                // Define columns in the exact order we want
                entity.Property(e => e.position_uuid);
                entity.Property(e => e.position_type).HasMaxLength(10);
                entity.Property(e => e.asset_pair).HasMaxLength(20);
                entity.Property(e => e.acquired_price).HasPrecision(18, 8);
                entity.Property(e => e.acquired_quantity).HasPrecision(18, 8);
                entity.Property(e => e.total_commissions).HasPrecision(18, 8);
                entity.Property(e => e.profit_loss).HasPrecision(18, 8);
                entity.Property(e => e.percentage_return).HasPrecision(18, 8);
                entity.Property(e => e.leftover_quantity).HasPrecision(18, 8);
                entity.Property(e => e.is_position_closed);
                entity.Property(e => e.last_updated);

                // Create the index
                entity.HasIndex(e => new { e.asset_pair, e.is_position_closed, e.leftover_quantity })
                    .HasFilter("leftover_quantity > 0")
                    .HasDatabaseName("IX_Trade_Records_asset_pair_is_position_closed_leftover_quantity");
            });

            modelBuilder.Entity<OpeningTrades>(entity =>
            {
                entity.ToTable("Opening_Trades", "public");
                entity.HasKey(e => e.trade_id);
                
                entity.Property(e => e.trade_id).HasMaxLength(255);
                entity.Property(e => e.side).HasMaxLength(4);
                entity.Property(e => e.asset_pair).HasMaxLength(20);
                entity.Property(e => e.acquired_quantity).HasPrecision(18, 8);
                entity.Property(e => e.acquired_price).HasPrecision(18, 8);
                entity.Property(e => e.commission).HasPrecision(18, 8);
                entity.Property(e => e.trade_time);

                entity.HasOne<TradeRecords>()
                    .WithMany()
                    .HasForeignKey(e => e.position_uuid)
                    .HasConstraintName("FK_Opening_Trades_Trade_Records_position_uuid");
            });

            modelBuilder.Entity<ClosingTrades>(entity =>
            {
                entity.ToTable("Closing_Trades", "public");
                entity.HasKey(e => e.trade_id);
                
                entity.Property(e => e.trade_id).HasMaxLength(255);
                entity.Property(e => e.side).HasMaxLength(4);
                entity.Property(e => e.asset_pair).HasMaxLength(20);
                entity.Property(e => e.offloaded_quantity).HasPrecision(18, 8);
                entity.Property(e => e.offloaded_price).HasPrecision(18, 8);
                entity.Property(e => e.commission).HasPrecision(18, 8);
                entity.Property(e => e.trade_time);

                entity.HasOne<TradeRecords>()
                    .WithMany()
                    .HasForeignKey(e => e.position_uuid)
                    .HasConstraintName("FK_Closing_Trades_Trade_Records_position_uuid");

                entity.HasOne<OpeningTrades>()
                    .WithMany()
                    .HasForeignKey(e => e.opening_trade_id)
                    .HasConstraintName("FK_Closing_Trades_Opening_Trades_opening_trade_id");
            });
        }
    }

    [Table("Trade_Records", Schema = "public")]
    public class TradeRecords
    {
        public Guid position_uuid { get; set; }
        public string position_type { get; set; } = string.Empty;
        public string asset_pair { get; set; } = string.Empty;
        public decimal acquired_price { get; set; }
        public decimal acquired_quantity { get; set; }
        public decimal total_commissions { get; set; }
        public decimal profit_loss { get; set; }
        public decimal percentage_return { get; set; }
        public decimal leftover_quantity { get; set; }
        public bool is_position_closed { get; set; }
        public DateTime last_updated { get; set; }
    }

    [Table("Opening_Trades", Schema = "public")]
    public class OpeningTrades
    {
        public string trade_id { get; set; } = string.Empty;
        public string side { get; set; } = string.Empty;
        public Guid position_uuid { get; set; }
        public string asset_pair { get; set; } = string.Empty;
        public decimal acquired_quantity { get; set; }
        public decimal acquired_price { get; set; }
        public decimal commission { get; set; }
        public DateTime trade_time { get; set; }
    }

    [Table("Closing_Trades", Schema = "public")]
    public class ClosingTrades
    {
        public string trade_id { get; set; } = string.Empty;
        public string side { get; set; } = string.Empty;
        public Guid position_uuid { get; set; }
        public string opening_trade_id { get; set; } = string.Empty;
        public string asset_pair { get; set; } = string.Empty;
        public decimal offloaded_quantity { get; set; }
        public decimal offloaded_price { get; set; }
        public decimal commission { get; set; }
        public DateTime trade_time { get; set; }
    }
}