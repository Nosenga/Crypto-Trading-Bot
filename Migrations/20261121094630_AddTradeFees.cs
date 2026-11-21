using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading_Bot_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddTradeFees : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Fees",
                table: "Trades",
                type: "decimal(18,8)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.UpdateData(
                table: "Portfolio",
                keyColumn: "Id",
                keyValue: 1,
                column: "LastUpdated",
                value: new DateTime(2026, 11, 21, 9, 46, 29, 730, DateTimeKind.Utc).AddTicks(2920));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 46, 29, 730, DateTimeKind.Utc).AddTicks(3100));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 46, 29, 730, DateTimeKind.Utc).AddTicks(3110));

            migrationBuilder.UpdateData(
                table: "Strategies",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 11, 21, 9, 46, 29, 730, DateTimeKind.Utc).AddTicks(3114));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Fees",
                table: "Trades");

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
        }
    }
}
