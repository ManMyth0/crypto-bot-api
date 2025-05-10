using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class TradeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TradeRecords",
                columns: table => new
                {
                    TradeId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TradeTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcquiredPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    AcquiredQuantity = table.Column<decimal>(type: "numeric", nullable: true),
                    SoldPrice = table.Column<decimal>(type: "numeric", nullable: true),
                    OffloadTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsSuccessful = table.Column<bool>(type: "boolean", nullable: true),
                    ProfitLoss = table.Column<decimal>(type: "numeric", nullable: true),
                    TradeType = table.Column<string>(type: "text", nullable: true),
                    PercentageOfReturn = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRecords", x => x.TradeId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeRecords");
        }
    }
}
