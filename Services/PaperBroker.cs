using System.Collections.Concurrent;
using TradingBot.Core.Interfaces;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class PaperBroker : IBrokerClient
    {
        private readonly ISafetyService _safety;

        public PaperBroker(ISafetyService safety)
        {
            _safety = safety;
        }

        // Phase 0: ConcurrentDictionary — GetCurrentPriceAsync is called from
        // LivePriceService (background) and from request threads at the same
        // time. A plain Dictionary corrupts under concurrent load.
        // Phase 0: prices are FROZEN at these seeds. The old code mutated the
        // price on every read, so SL/TP checks moved the market they were
        // checking. Real data arrives in Phase 2 (Binance klines); until
        // then, stable prices beat a fake random walk.
        private static readonly ConcurrentDictionary<string, decimal> _livePrices = new(
            new Dictionary<string, decimal>
            {
                ["BTCUSDT"] = 43500m,
                ["ETHUSDT"] = 2650m,
                ["BNBUSDT"] = 310m,
                ["SOLUSDT"] = 95m,
                ["ADAUSDT"] = 0.45m,
                ["XRPUSDT"] = 0.55m,
                ["DOTUSDT"] = 6.50m,
                ["DOGEUSDT"] = 0.10m
            });

        // Simulated account balance (Paper trading keeps a separate balance
        // from your real DB).
        private decimal _paperBalance = 100000m;

        public Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            // Phase 0: read-only. No mutation on read.
            var price = _livePrices.GetOrAdd(symbol, 100m);
            return Task.FromResult(price);
        }

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