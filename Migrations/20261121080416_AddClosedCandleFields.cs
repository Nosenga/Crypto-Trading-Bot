using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading_Bot_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddClosedCandleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CloseTime",
                table: "MarketData",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "MarketData",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CloseTime",
                table: "MarketData");

            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "MarketData");

            migrationBuilder.UpdateData(
                table: "Portfolio",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4640));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4792));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4802));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4806));
        }
    }
}
