using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Trading_Bot_Website.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BacktestResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    InitialCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalCapital = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalReturn = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalReturnPercent = table.Column<double>(type: "REAL", nullable: false),
                    AnnualizedReturn = table.Column<double>(type: "REAL", nullable: false),
                    SharpeRatio = table.Column<double>(type: "REAL", nullable: false),
                    MaxDrawdownPercent = table.Column<double>(type: "REAL", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinRate = table.Column<double>(type: "REAL", nullable: false),
                    LargestWin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LargestLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageWin = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AverageLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EquityCurveJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    TradeLogJson = table.Column<string>(type: "TEXT", maxLength: 5000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SMA20 = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    SMA50 = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    EMA12 = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    EMA26 = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    RSI = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    MACDLine = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    MACDSignal = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    BollingerUpper = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    BollingerLower = table.Column<decimal>(type: "decimal(18,8)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketData", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Portfolio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    InitialBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalEquity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UnrealizedPnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RealizedPnL = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUpdated = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Portfolio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioHoldings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    AverageEntryPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    CurrentPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioHoldings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Signals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    TargetPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    StopLossPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsExecuted = table.Column<bool>(type: "INTEGER", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Signals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Strategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StopLossPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TakeProfitPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RiskPerTradePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxPositionSize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Parameters = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    TotalTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    WinningTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    LosingTrades = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalProfitLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MaxDrawdown = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderType = table.Column<int>(type: "INTEGER", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ProfitLoss = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    TotalValue = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Strategy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExecutedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SignalSource = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Confidence = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Portfolio",
                columns: new[] { "Id", "CurrentBalance", "InitialBalance", "LastUpdated", "LosingTrades", "MaxDrawdown", "RealizedPnL", "TotalEquity", "TotalTrades", "UnrealizedPnL", "WinningTrades" },
                values: new object[] { 1, 1000000m, 1000000m, new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4640), 0, 0m, 0m, 1000000m, 0, 0m, 0 });

            migrationBuilder.InsertData(
                table: "Strategies",
                columns: new[] { "Id", "CreatedAt", "Description", "LastExecutedAt", "LosingTrades", "MaxDrawdown", "MaxPositionSize", "Name", "Parameters", "RiskPerTradePercent", "Status", "StopLossPercent", "Symbol", "TakeProfitPercent", "TotalProfitLoss", "TotalTrades", "Type", "WinningTrades" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4792), "Buy when SMA20 crosses above SMA50, sell when it crosses below", null, 0, 0m, 10000m, "MA Crossover 20/50", "{\"ShortPeriod\":20,\"LongPeriod\":50}", 1m, 0, 2m, "BTCUSDT", 5m, 0m, 0, 0, 0 },
                    { 2, new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4802), "Buy when RSI < 30, sell when RSI > 70", null, 0, 0m, 10000m, "RSI Overbought/Oversold", "{\"OversoldLevel\":30,\"OverboughtLevel\":70,\"Period\":14}", 1.0m, 0, 3m, "BTCUSDT", 6m, 0m, 0, 1, 0 },
                    { 3, new DateTime(2026, 11, 21, 7, 52, 55, 24, DateTimeKind.Utc).AddTicks(4806), "Buy/sell on MACD line crossing signal line", null, 0, 0m, 10000m, "MACD Crossover", "{\"FastPeriod\":12,\"SlowPeriod\":26,\"SignalPeriod\":9}", 1.0m, 0, 2.5m, "BTCUSDT", 7m, 0m, 0, 2, 0 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketData_Symbol_Timestamp",
                table: "MarketData",
                columns: new[] { "Symbol", "Timestamp" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Signals_Symbol_GeneratedAt",
                table: "Signals",
                columns: new[] { "Symbol", "GeneratedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Symbol_Status",
                table: "Trades",
                columns: new[] { "Symbol", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BacktestResults");

            migrationBuilder.DropTable(
                name: "MarketData");

            migrationBuilder.DropTable(
                name: "Portfolio");

            migrationBuilder.DropTable(
                name: "PortfolioHoldings");

            migrationBuilder.DropTable(
                name: "Signals");

            migrationBuilder.DropTable(
                name: "Strategies");

            migrationBuilder.DropTable(
                name: "Trades");
        }
    }
}
