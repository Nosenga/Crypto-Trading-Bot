using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Interfaces;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class PaperBroker : IBrokerClient
    {
        private readonly ISafetyService _safety;
        private readonly IPriceFeed _priceFeed;
        private readonly ConcurrentDictionary<string, (decimal StepSize, decimal MinNotional, decimal TickSize)> _filters =
            new(StringComparer.OrdinalIgnoreCase);

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

        public async Task RefreshFiltersAsync(IEnumerable<string> symbols)
        {
            var refreshedFilters = await _priceFeed.GetExchangeFiltersAsync(symbols);
            foreach (var filter in refreshedFilters)
                _filters[filter.Key] = filter.Value;
        }

        public Task InitializeAsync(IEnumerable<string> symbols) =>
            RefreshFiltersAsync(symbols);

        public async Task<decimal> NormalizeQuantityAsync(
            string symbol, decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Order quantity must be greater than zero.");

            await EnsureFiltersLoadedAsync(symbol);
            return RoundQuantity(symbol, quantity);
        }

        public async Task<(decimal StopPrice, decimal LimitPrice)> NormalizeOcoPricesAsync(
            string symbol, decimal stopPrice, decimal limitPrice)
        {
            if (stopPrice <= 0 || limitPrice <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(stopPrice), "OCO stop and limit prices must be greater than zero.");

            await EnsureFiltersLoadedAsync(symbol);
            return (RoundPriceUp(symbol, stopPrice), RoundPriceDown(symbol, limitPrice));
        }

        private async Task EnsureFiltersLoadedAsync(string symbol)
        {
            if (!_filters.ContainsKey(symbol))
                await RefreshFiltersAsync(new[] { symbol });
        }

        private decimal RoundQuantity(string symbol, decimal quantity)
        {
            if (!_filters.TryGetValue(symbol, out var filter))
                throw new InvalidOperationException(
                    $"Exchange filters for {symbol} have not been loaded.");

            if (filter.StepSize <= 0)
                throw new InvalidOperationException(
                    $"Exchange filter for {symbol} has an invalid step size.");

            var steps = Math.Floor(quantity / filter.StepSize);
            return steps * filter.StepSize;
        }

        private decimal RoundPriceDown(string symbol, decimal price)
        {
            if (!_filters.TryGetValue(symbol, out var filter) || filter.TickSize <= 0)
                throw new InvalidOperationException(
                    $"Exchange price tick for {symbol} is unavailable.");

            return Math.Floor(price / filter.TickSize) * filter.TickSize;
        }

        private decimal RoundPriceUp(string symbol, decimal price)
        {
            if (!_filters.TryGetValue(symbol, out var filter) || filter.TickSize <= 0)
                throw new InvalidOperationException(
                    $"Exchange price tick for {symbol} is unavailable.");

            return Math.Ceiling(price / filter.TickSize) * filter.TickSize;
        }

        private void ValidateNotional(string symbol, decimal quantity, decimal price)
        {
            if (!_filters.TryGetValue(symbol, out var filter))
                throw new InvalidOperationException(
                    $"Exchange filters for {symbol} have not been loaded.");

            var notional = quantity * price;
            if (notional < filter.MinNotional)
            {
                throw new InvalidOperationException(
                    $"Order notional (${notional:F2}) is below the minimum required " +
                    $"(${filter.MinNotional}) for {symbol}.");
            }
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

        public async Task<string> PlaceMarketOrderAsync(
            string symbol, string side, decimal quantity)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Order quantity must be greater than zero.");

            await EnsureFiltersLoadedAsync(symbol);
            quantity = RoundQuantity(symbol, quantity);
            if (quantity <= 0)
                throw new InvalidOperationException(
                    $"Quantity too small after rounding for {symbol}.");

            var price = await GetCurrentPriceAsync(symbol);
            if (price <= 0)
                throw new InvalidOperationException(
                    $"Could not determine a valid current price for {symbol}.");

            ValidateNotional(symbol, quantity, price);

            var orderId = Guid.NewGuid().ToString();
            return orderId;
        }

        public async Task<string> PlaceClosingMarketOrderAsync(
            string symbol, string side, decimal quantity)
        {
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Order quantity must be greater than zero.");

            await EnsureFiltersLoadedAsync(symbol);
            var roundedQuantity = RoundQuantity(symbol, quantity);
            if (roundedQuantity != quantity)
                throw new InvalidOperationException(
                    $"Order quantity for {symbol} is not aligned with the exchange step size; " +
                    $"rounded-down quantity would be {roundedQuantity}.");
            quantity = roundedQuantity;

            var price = await GetCurrentPriceAsync(symbol);
            if (price <= 0)
                throw new InvalidOperationException(
                    $"Could not determine a valid current price for {symbol}.");

            ValidateNotional(symbol, quantity, price);

            var orderId = Guid.NewGuid().ToString();
            return orderId;
        }

        public async Task<string> PlaceLimitOrderAsync(
            string symbol, string side, decimal quantity, decimal price)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(quantity), "Order quantity must be greater than zero.");

            if (price <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(price), "Limit price must be greater than zero.");

            await EnsureFiltersLoadedAsync(symbol);
            quantity = RoundQuantity(symbol, quantity);
            if (quantity <= 0)
                throw new InvalidOperationException(
                    $"Quantity too small after rounding for {symbol}.");

            ValidateNotional(symbol, quantity, price);

            var orderId = Guid.NewGuid().ToString();
            return orderId;
        }

        public Task<List<Trade>> GetOpenExchangePositionsAsync() =>
            Task.FromResult(new List<Trade>());

        public Task<string> PlaceOcoOrderAsync(
            string symbol, string side, decimal quantity,
            decimal stopPrice, decimal limitPrice)
        {
            if (_safety.IsKillSwitchActive)
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

            return Task.FromResult($"PAPER_OCO_{Guid.NewGuid()}");
        }

        public Task SyncOpenTradesAsync(AppDbContext db, ILogger logger) =>
            Task.CompletedTask;

        public Task<bool> CancelOrderAsync(string orderId)
        {
            return Task.FromResult(true);
        }

        public Task<bool> CancelOcoAsync(string symbol, string listClientOrderId)
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