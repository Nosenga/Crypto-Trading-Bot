using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Trading_Bot_Website.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioRiskManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "HighWaterMark",
                table: "Portfolio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRiskCheckDate",
                table: "Portfolio",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "StartOfDayBalance",
                table: "Portfolio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE Portfolio " +
                "SET HighWaterMark = InitialBalance, " +
                "StartOfDayBalance = CurrentBalance;");

            migrationBuilder.UpdateData(
                table: "Portfolio",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "HighWaterMark", "LastRiskCheckDate", "StartOfDayBalance" },
                values: new object[] { 1000000m, new DateTime(2026, 11, 21, 0, 0, 0, 0, DateTimeKind.Utc), 1000000m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HighWaterMark",
                table: "Portfolio");

            migrationBuilder.DropColumn(
                name: "LastRiskCheckDate",
                table: "Portfolio");

            migrationBuilder.DropColumn(
                name: "StartOfDayBalance",
                table: "Portfolio");
        }
    }
}
