using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTradeRecordsPrimaryKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeRecords",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "trade_id",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "trade_time",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.AlterColumn<Guid>(
                name: "position_uuid",
                schema: "public",
                table: "TradeRecords",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeRecords",
                schema: "public",
                table: "TradeRecords",
                column: "position_uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_TradeRecords",
                schema: "public",
                table: "TradeRecords");

            migrationBuilder.AlterColumn<Guid>(
                name: "position_uuid",
                schema: "public",
                table: "TradeRecords",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "trade_id",
                schema: "public",
                table: "TradeRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<DateTime>(
                name: "trade_time",
                schema: "public",
                table: "TradeRecords",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TradeRecords",
                schema: "public",
                table: "TradeRecords",
                column: "trade_id");
        }
    }
}
