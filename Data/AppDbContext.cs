// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using TradingBot.Models;

namespace TradingBot.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options) { }

        public DbSet<Trade> Trades { get; set; }
        public DbSet<Strategy> Strategies { get; set; }
        public DbSet<Portfolio> Portfolio { get; set; }
        public DbSet<PortfolioHolding> PortfolioHoldings { get; set; }
        public DbSet<Signal> Signals { get; set; }
        public DbSet<MarketData> MarketData { get; set; }
        public DbSet<BacktestResult> BacktestResults { get; set; }
        public DbSet<NewsSentiment> NewsSentiments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<MarketData>()
                .HasIndex(m => new { m.Symbol, m.Timestamp })
                .IsUnique();

            modelBuilder.Entity<Trade>()
                .HasIndex(t => new { t.Symbol, t.Status });

            modelBuilder.Entity<Signal>()
                .HasIndex(s => new { s.Symbol, s.Source, s.CandleTimestamp })
                .IsUnique();

            // Seed default portfolio
            modelBuilder.Entity<Portfolio>().HasData(
                new Portfolio
                {
                    Id = 1,
                    InitialBalance = 1000000m,
                    CurrentBalance = 1000000m,
                    TotalEquity = 1000000m, // Phase 1: was 100000m — factor-of-10 typo
                    HighWaterMark = 1000000m,
                    StartOfDayBalance = 1000000m,
                    StartOfDayEquity = 1000000m,
                    LastRiskCheckDate = new DateTime(2026, 11, 21, 0, 0, 0, DateTimeKind.Utc),
                    LastUpdated = new DateTime(2026, 11, 21, 13, 14, 11, 229, DateTimeKind.Utc)
                        .AddTicks(5018)
                }
            );

            // Seed default strategies
            modelBuilder.Entity<Strategy>().HasData(
                new Strategy
                {
                    Id = 1,
                    Name = "MA Crossover 20/50",
                    Description = "Buy when SMA20 crosses above SMA50, sell when it crosses below",
                    Type = StrategyType.MovingAverageCross,
                    Symbol = "BTCUSDT",
                    StopLossPercent = 2m,
                    TakeProfitPercent = 5m,
                    RiskPerTradePercent = 1m,
                    Parameters = "{\"ShortPeriod\":20,\"LongPeriod\":50}",
                    CreatedAt = new DateTime(2026, 11, 21, 13, 14, 11, 229, DateTimeKind.Utc)
                        .AddTicks(5177)
                },
                new Strategy
                {
                    Id = 2,
                    Name = "RSI Overbought/Oversold",
                    Description = "Buy when RSI < 30, sell when RSI > 70",
                    Type = StrategyType.RSI,
                    Symbol = "BTCUSDT",
                    StopLossPercent = 3m,
                    TakeProfitPercent = 6m,
                    Parameters = "{\"OversoldLevel\":30,\"OverboughtLevel\":70,\"Period\":14}",
                    CreatedAt = new DateTime(2026, 11, 21, 13, 14, 11, 229, DateTimeKind.Utc)
                        .AddTicks(5184)
                },
                new Strategy
                {
                    Id = 3,
                    Name = "MACD Crossover",
                    Description = "Buy/sell on MACD line crossing signal line",
                    Type = StrategyType.MACD,
                    Symbol = "BTCUSDT",
                    StopLossPercent = 2.5m,
                    TakeProfitPercent = 7m,
                    Parameters = "{\"FastPeriod\":12,\"SlowPeriod\":26,\"SignalPeriod\":9}",
                    CreatedAt = new DateTime(2026, 11, 21, 13, 14, 11, 229, DateTimeKind.Utc)
                        .AddTicks(5187)
                }
            );
        }
    }
}