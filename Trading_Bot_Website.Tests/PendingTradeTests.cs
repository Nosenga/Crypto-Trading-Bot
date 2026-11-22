using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TradingBot.Core.Interfaces;
using TradingBot.Data;
using TradingBot.Models;
using TradingBot.Services;
using Xunit;

namespace Trading_Bot_Website.Tests;

public sealed class PendingTradeTests
{
    [Fact]
    public async Task Unknown_market_order_outcome_keeps_trade_pending_without_deducting_balance()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var startingBalance = (await context.Portfolio.SingleAsync()).CurrentBalance;
        var broker = new TestBroker
        {
            MarketOrderException = new TimeoutException("Broker response timed out.")
        };
        var service = CreateTradingService(context, broker);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteTradeAsync("BTCUSDT", TradeType.Buy, 0.001m));

        context.ChangeTracker.Clear();
        var trade = await context.Trades.SingleAsync();
        var portfolio = await context.Portfolio.SingleAsync();

        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.Equal(TradeStatus.Pending, trade.Status);
        Assert.StartsWith("PENDING_RECONCILIATION:", trade.Notes);
        Assert.Equal(startingBalance, portfolio.CurrentBalance);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    [Fact]
    public async Task Definite_market_order_rejection_cancels_pending_trade()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var startingBalance = (await context.Portfolio.SingleAsync()).CurrentBalance;
        var broker = new TestBroker
        {
            MarketOrderException = new OrderRejectedException("Insufficient funds.")
        };
        var service = CreateTradingService(context, broker);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExecuteTradeAsync("BTCUSDT", TradeType.Buy, 0.001m));

        context.ChangeTracker.Clear();
        var trade = await context.Trades.SingleAsync();
        var portfolio = await context.Portfolio.SingleAsync();

        Assert.Equal(TradeStatus.Cancelled, trade.Status);
        Assert.StartsWith("REJECTED:", trade.Notes);
        Assert.Null(trade.ExecutedAt);
        Assert.Equal(startingBalance, portfolio.CurrentBalance);
        Assert.Equal(1, broker.MarketOrderCalls);
    }

    private static TradingBotService CreateTradingService(
        AppDbContext context, TestBroker broker)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["TradingBot:MaxPositionSize"] = "1000",
                ["TradingBot:MaxOpenPositions"] = "10"
            })
            .Build();

        return new TradingBotService(
            context,
            new TestMarketDataService(),
            broker,
            configuration,
            NullLogger<TradingBotService>.Instance,
            new TestSafetyService { IsArmed = true });
    }

    private sealed class TestBroker : IBrokerClient
    {
        public Exception? MarketOrderException { get; init; }
        public int MarketOrderCalls { get; private set; }

        public Task InitializeAsync(IEnumerable<string> symbols) => Task.CompletedTask;
        public Task<decimal> NormalizeQuantityAsync(string symbol, decimal quantity) =>
            Task.FromResult(quantity);
        public Task<decimal> GetCurrentPriceAsync(string symbol) =>
            Task.FromResult(50_000m);
        public Task<List<MarketData>> GetHistoricalCandlesAsync(
            string symbol, DateTime from, DateTime to, string timeframe = "1h") =>
            Task.FromResult(new List<MarketData>());
        public Task<decimal> GetAvailableBalanceAsync(string quoteCurrency) =>
            Task.FromResult(1_000_000m);

        public Task<string> PlaceMarketOrderAsync(
            string symbol, string side, decimal quantity)
        {
            MarketOrderCalls++;
            return MarketOrderException is null
                ? Task.FromResult("test-order")
                : Task.FromException<string>(MarketOrderException);
        }

        public Task<string> PlaceClosingMarketOrderAsync(
            string symbol, string side, decimal quantity) =>
            Task.FromResult("test-close-order");
        public Task<string> PlaceLimitOrderAsync(
            string symbol, string side, decimal quantity, decimal price) =>
            Task.FromResult("test-limit-order");
        public Task<List<Trade>> GetOpenExchangePositionsAsync() =>
            Task.FromResult(new List<Trade>());
        public Task<string> PlaceOcoOrderAsync(
            string symbol, string side, decimal quantity,
            decimal stopPrice, decimal limitPrice) =>
            Task.FromResult("test-oco");
        public Task<(decimal StopPrice, decimal LimitPrice)> NormalizeOcoPricesAsync(
            string symbol, decimal stopPrice, decimal limitPrice) =>
            Task.FromResult((stopPrice, limitPrice));
        public Task SyncOpenTradesAsync(AppDbContext db, ILogger logger) =>
            Task.CompletedTask;
        public Task<bool> CancelOrderAsync(string orderId) => Task.FromResult(true);
        public Task<bool> CancelOcoAsync(string symbol, string listClientOrderId) =>
            Task.FromResult(true);
        public Task<object> GetOrderStatusAsync(string orderId) =>
            Task.FromResult<object>(new { Id = orderId, Status = "FILLED" });
    }

    private sealed class TestMarketDataService : IMarketDataService
    {
        public Task<List<MarketData>> GetHistoricalDataAsync(
            string symbol, DateTime from, DateTime to) =>
            Task.FromResult(new List<MarketData>());
        public Task<MarketData?> GetLatestPriceAsync(string symbol) =>
            Task.FromResult<MarketData?>(null);
        public Task<Dictionary<string, decimal>> GetPriceUpdatesAsync(
            List<string> symbols) =>
            Task.FromResult(new Dictionary<string, decimal>());
        public Task<List<MarketData>> CalculateIndicatorsAsync(List<MarketData> data) =>
            Task.FromResult(data);
        public Task RefreshRecentCandlesAsync(
            string symbol, string timeframe = "1m", int limit = 100) =>
            Task.CompletedTask;
    }

    private sealed class TestSafetyService : ISafetyService
    {
        public bool IsArmed { get; set; }
        public bool IsKillSwitchActive { get; private set; }
        public DateTime? KillSwitchAt { get; private set; }
        public string? KillSwitchReason { get; private set; }

        public void ArmTrading() => IsArmed = true;
        public void DisarmTrading() => IsArmed = false;
        public void ActivateKillSwitch(string reason)
        {
            IsKillSwitchActive = true;
            KillSwitchReason = reason;
            KillSwitchAt = DateTime.UtcNow;
        }
        public void DeactivateKillSwitch()
        {
            IsKillSwitchActive = false;
            KillSwitchReason = null;
            KillSwitchAt = null;
        }
        public void UpdateHighWaterMark(Portfolio portfolio)
        {
            if (portfolio.TotalEquity > portfolio.HighWaterMark)
                portfolio.HighWaterMark = portfolio.TotalEquity;
        }
    }
}
