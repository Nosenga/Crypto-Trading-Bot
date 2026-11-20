using TradingBot.Core.Interfaces;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class PaperBroker : IBrokerClient
    {
        private readonly Random _random = new();
        private readonly ISafetyService _safety;

        public PaperBroker(ISafetyService safety)
        {
            _safety = safety;
        }
        
        // Simulated in-memory prices (matching your current MarketDataService)
        private static readonly Dictionary<string, decimal> _livePrices = new()
        {
            ["BTCUSDT"] = 43500m,
            ["ETHUSDT"] = 2650m,
            ["BNBUSDT"] = 310m,
            ["SOLUSDT"] = 95m,
            ["ADAUSDT"] = 0.45m,
            ["XRPUSDT"] = 0.55m,
            ["DOTUSDT"] = 6.50m,
            ["DOGEUSDT"] = 0.10m
        };

        // Simulated account balance (Paper trading keeps a separate balance from your real DB)
        private decimal _paperBalance = 100000m; 

        public Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            if (!_livePrices.ContainsKey(symbol))
            {
                // If the symbol isn't in our dictionary, start it at $100
                _livePrices[symbol] = 100m;
            }

            // Simulate realistic price fluctuation (Random walk)
            var change = (decimal)(_random.NextDouble() * 0.01 - 0.005);
            _livePrices[symbol] *= (1 + change);
            _livePrices[symbol] = Math.Round(_livePrices[symbol], 8); // Keep precision for crypto

            return Task.FromResult(_livePrices[symbol]);
        }

        public Task<List<MarketData>> GetHistoricalCandlesAsync(string symbol, DateTime from, DateTime to, string timeframe = "1h")
        {
            // This generates the exact same simulated data your MarketDataService already has.
            // You can just call your existing MarketDataService.GenerateSimulatedDataAsync here,
            // but for simplicity, we'll just return an empty list (it will force the backtest to simulate it).
            return Task.FromResult(new List<MarketData>());
        }

        public Task<decimal> GetAvailableBalanceAsync(string quoteCurrency)
        {
            // Return the paper balance. You can modify this in memory to simulate deposits/withdrawals.
            return Task.FromResult(_paperBalance);
        }

        public Task<string> PlaceMarketOrderAsync(string symbol, string side, decimal quantity)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            // Simulate a market order being accepted by the exchange
            var orderId = Guid.NewGuid().ToString();
            
            // Optional: Update the paper balance instantly to simulate a fill
            // (You can also let your TradingBotService handle the balance update, which is actually safer)
            return Task.FromResult(orderId);
        }

        public Task<string> PlaceLimitOrderAsync(string symbol, string side, decimal quantity, decimal price)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            // Simulate a limit order being placed
            var orderId = Guid.NewGuid().ToString();
            return Task.FromResult(orderId);
        }

        public Task<bool> CancelOrderAsync(string orderId)
        {
            // Simulate a cancellation (always succeeds in paper trading)
            return Task.FromResult(true);
        }

        public Task<object> GetOrderStatusAsync(string orderId)
        {
            // Returns a generic object representing the order status.
            // In a real broker, this would be a specific object from Binance/Oanda.
            return Task.FromResult<object>(new
            {
                Id = orderId,
                Status = "FILLED",
                ExecutedQuantity = 0m, // Would be filled quantity in real life
                Price = 0m
            });
        }
    }
}