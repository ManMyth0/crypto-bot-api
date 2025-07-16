using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductInfoTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductInfo",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    ProductId = table.Column<string>(type: "text", nullable: false),
                    BaseCurrency = table.Column<string>(type: "text", nullable: false),
                    QuoteCurrency = table.Column<string>(type: "text", nullable: false),
                    QuoteIncrement = table.Column<decimal>(type: "numeric", nullable: false),
                    BaseIncrement = table.Column<decimal>(type: "numeric", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    MinMarketFunds = table.Column<decimal>(type: "numeric", nullable: false),
                    MarginEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PostOnly = table.Column<bool>(type: "boolean", nullable: false),
                    LimitOnly = table.Column<bool>(type: "boolean", nullable: false),
                    CancelOnly = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StatusMessage = table.Column<string>(type: "text", nullable: false),
                    TradingDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    FxStablecoin = table.Column<bool>(type: "boolean", nullable: false),
                    MaxSlippagePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    AuctionMode = table.Column<bool>(type: "boolean", nullable: false),
                    HighBidLimitPercentage = table.Column<string>(type: "text", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductInfo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductInfo_ProductId",
                table: "ProductInfo",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductInfo");
        }
    }
}
