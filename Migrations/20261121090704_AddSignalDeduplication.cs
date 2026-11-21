using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading_Bot_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalDeduplication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Signals_Symbol_GeneratedAt",
                table: "Signals");

            migrationBuilder.AddColumn<DateTime>(
                name: "CandleTimestamp",
                table: "Signals",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "Portfolio",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 11, 21, 9, 7, 4, 306, DateTimeKind.Utc).AddTicks(4793));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 7, 4, 306, DateTimeKind.Utc).AddTicks(4981));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 7, 4, 306, DateTimeKind.Utc).AddTicks(5082));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 7, 4, 306, DateTimeKind.Utc).AddTicks(5086));

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Symbol_Source_CandleTimestamp",
                table: "Signals",
                columns: new[] { "Symbol", "Source", "CandleTimestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Signals_Symbol_Source_CandleTimestamp",
                table: "Signals");

            migrationBuilder.DropColumn(
                name: "CandleTimestamp",
                table: "Signals");

            migrationBuilder.UpdateData(
                table: "Portfolio",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 11, 21, 8, 4, 15, 949, DateTimeKind.Utc).AddTicks(6538));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 8, 4, 15, 949, DateTimeKind.Utc).AddTicks(6730));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 8, 4, 15, 949, DateTimeKind.Utc).AddTicks(6738));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 8, 4, 15, 949, DateTimeKind.Utc).AddTicks(6793));

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Symbol_GeneratedAt",
                table: "Signals",
                columns: new[] { "Symbol", "GeneratedAt" });
        }
    }
}
