using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "Trade_Records",
                schema: "public",
                columns: table => new
                {
                    position_uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    position_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    asset_pair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acquired_price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    acquired_quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    total_commissions = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    profit_loss = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    percentage_return = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    leftover_quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    is_position_closed = table.Column<bool>(type: "boolean", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trade_Records", x => x.position_uuid);
                });

            migrationBuilder.CreateTable(
                name: "Opening_Trades",
                schema: "public",
                columns: table => new
                {
                    trade_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    side = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    position_uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    asset_pair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    acquired_quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    acquired_price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    commission = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    trade_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opening_Trades", x => x.trade_id);
                    table.ForeignKey(
                        name: "FK_Opening_Trades_Trade_Records_position_uuid",
                        column: x => x.position_uuid,
                        principalSchema: "public",
                        principalTable: "Trade_Records",
                        principalColumn: "position_uuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Closing_Trades",
                schema: "public",
                columns: table => new
                {
                    trade_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    side = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false),
                    position_uuid = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_trade_id = table.Column<string>(type: "character varying(255)", nullable: false),
                    asset_pair = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    offloaded_quantity = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    offloaded_price = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    commission = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    trade_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Closing_Trades", x => x.trade_id);
                    table.ForeignKey(
                        name: "FK_Closing_Trades_Opening_Trades_opening_trade_id",
                        column: x => x.opening_trade_id,
                        principalSchema: "public",
                        principalTable: "Opening_Trades",
                        principalColumn: "trade_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Closing_Trades_Trade_Records_position_uuid",
                        column: x => x.position_uuid,
                        principalSchema: "public",
                        principalTable: "Trade_Records",
                        principalColumn: "position_uuid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Closing_Trades_opening_trade_id",
                schema: "public",
                table: "Closing_Trades",
                column: "opening_trade_id");

            migrationBuilder.CreateIndex(
                name: "IX_Closing_Trades_position_uuid",
                schema: "public",
                table: "Closing_Trades",
                column: "position_uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Opening_Trades_position_uuid",
                schema: "public",
                table: "Opening_Trades",
                column: "position_uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Trade_Records_asset_pair_is_position_closed_leftover_quantity",
                schema: "public",
                table: "Trade_Records",
                columns: new[] { "asset_pair", "is_position_closed", "leftover_quantity" },
                filter: "leftover_quantity > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Closing_Trades",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Opening_Trades",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Trade_Records",
                schema: "public");
        }
    }
}
