using TradingBot.Core.Interfaces;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class PaperBroker : IBrokerClient
    {
        private readonly ISafetyService _safety;
        private readonly IPriceFeed _priceFeed;

        public PaperBroker(ISafetyService safety, IPriceFeed priceFeed)
        {
            _safety = safety;
            _priceFeed = priceFeed;
        }

        // Simulated account balance (Paper trading keeps a separate balance
        // from your real DB).
        private decimal _paperBalance = 100000m;

        public Task<decimal> GetCurrentPriceAsync(string symbol) =>
            _priceFeed.GetCurrentPriceAsync(symbol);

        public Task<List<MarketData>> GetHistoricalCandlesAsync(
            string symbol, DateTime from, DateTime to, string timeframe = "1h")
        {
            // Phase 2: replace with real Binance klines via MarketDataService.
            // Returning empty is honest — the old code returned fake data.
            return Task.FromResult(new List<MarketData>());
        }

        public Task<decimal> GetAvailableBalanceAsync(string quoteCurrency)
        {
            return Task.FromResult(_paperBalance);
        }

        public Task<string> PlaceMarketOrderAsync(string symbol, string side, decimal quantity)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            var orderId = Guid.NewGuid().ToString();
            return Task.FromResult(orderId);
        }

        public Task<string> PlaceClosingMarketOrderAsync(
            string symbol, string side, decimal quantity)
        {
            var orderId = Guid.NewGuid().ToString();
            return Task.FromResult(orderId);
        }

        public Task<string> PlaceLimitOrderAsync(
            string symbol, string side, decimal quantity, decimal price)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            var orderId = Guid.NewGuid().ToString();
            return Task.FromResult(orderId);
        }

        public Task<bool> CancelOrderAsync(string orderId)
        {
            return Task.FromResult(true);
        }

        public Task<object> GetOrderStatusAsync(string orderId)
        {
            return Task.FromResult<object>(new
            {
                Id = orderId,
                Status = "FILLED",
                ExecutedQuantity = 0m,
                Price = 0m
            });
        }
    }
}