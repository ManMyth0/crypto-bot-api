using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace crypto_bot_api.Migrations
{
    /// <inheritdoc />
    public partial class RenameLastUpdatedToRecordOpenedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_updated",
                schema: "public",
                table: "Trade_Records");

            migrationBuilder.AddColumn<DateTime>(
                name: "record_opened_at",
                schema: "public",
                table: "Trade_Records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "CURRENT_TIMESTAMP");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "record_opened_at",
                schema: "public",
                table: "Trade_Records");

            migrationBuilder.AddColumn<DateTime>(
                name: "last_updated",
                schema: "public",
                table: "Trade_Records",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
