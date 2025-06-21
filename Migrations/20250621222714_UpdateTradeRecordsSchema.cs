using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTradeRecordsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AcquiredPrice",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "AcquiredQuantity",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "IsSuccessful",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "OffloadTime",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "PercentageOfReturn",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "ProfitLoss",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "SoldPrice",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "TradeType",
                table: "TradeRecords");

            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.RenameTable(
                name: "TradeRecords",
                newName: "TradeRecords",
                newSchema: "public");

            migrationBuilder.RenameColumn(
                name: "TradeId",
                schema: "public",
                table: "TradeRecords",
                newName: "trade_id");

            migrationBuilder.RenameColumn(
                name: "TradeTime",
                schema: "public",
                table: "TradeRecords",
                newName: "trade_time");

            migrationBuilder.AddColumn<decimal>(
                name: "acquired_price",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "acquired_quantity",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "asset_pair",
                schema: "public",
                table: "TradeRecords",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_position_closed",
                schema: "public",
                table: "TradeRecords",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_updated",
                schema: "public",
                table: "TradeRecords",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "leftover_quantity",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "percentage_return",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "position_uuid",
                schema: "public",
                table: "TradeRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "profit_loss",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_commissions",
                schema: "public",
                table: "TradeRecords",
                type: "numeric(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeRecords_asset_pair_is_position_closed_leftover_quantity",
                schema: "public",
                table: "TradeRecords",
                columns: new[] { "asset_pair", "is_position_closed", "leftover_quantity" },
                filter: "leftover_quantity > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TradeRecords_asset_pair_is_position_closed_leftover_quantity",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "acquired_price",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "acquired_quantity",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "asset_pair",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "is_position_closed",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "last_updated",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "leftover_quantity",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "percentage_return",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "position_uuid",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "profit_loss",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "total_commissions",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.RenameTable(
                name: "TradeRecords",
                schema: "public",
                newName: "TradeRecords");

            migrationBuilder.RenameColumn(
                name: "trade_id",
                table: "TradeRecords",
                newName: "TradeId");

            migrationBuilder.RenameColumn(
                name: "trade_time",
                table: "TradeRecords",
                newName: "TradeTime");

            migrationBuilder.AddColumn<decimal>(
                name: "AcquiredPrice",
                table: "TradeRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AcquiredQuantity",
                table: "TradeRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSuccessful",
                table: "TradeRecords",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OffloadTime",
                table: "TradeRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PercentageOfReturn",
                table: "TradeRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ProfitLoss",
                table: "TradeRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SoldPrice",
                table: "TradeRecords",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeType",
                table: "TradeRecords",
                type: "text",
                nullable: true);
        }
    }
}
