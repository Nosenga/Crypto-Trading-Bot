using System.Collections.Concurrent;
using System.Threading;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Interfaces;
using TradingBot.Models;

namespace TradingBot.Services;

public class BinanceBroker : IBrokerClient
{
    private readonly ILogger<BinanceBroker> _logger;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly bool _enableLiveOrders;
    private readonly ISafetyService _safety;

    // Binance requires the symbol when cancelling or querying a Spot order.
    // This mapping is only retained while the application is running.
    private readonly ConcurrentDictionary<string, string> _orderSymbols = new();

    // Serializes order submissions. This is not a complete API rate limiter,
    // but it prevents simultaneous order submissions from this broker instance.
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);

    public BinanceBroker(
        ILogger<BinanceBroker> logger,
        IConfiguration configuration,
        ISafetyService safety)
    {
        _logger = logger;
        _safety = safety;

        _apiKey = configuration["TradingBot:Binance:ApiKey"]
            ?? throw new InvalidOperationException(
                "TradingBot:Binance:ApiKey is not configured.");

        _apiSecret = configuration["TradingBot:Binance:ApiSecret"]
            ?? throw new InvalidOperationException(
                "TradingBot:Binance:ApiSecret is not configured.");

        _enableLiveOrders = configuration.GetValue<bool>(
            "TradingBot:Binance:EnableLiveOrders");
    }

    private BinanceClient CreateClient()
    {
        return new BinanceClient(new BinanceClientOptions
        {
            ApiCredentials = new BinanceApiCredentials(_apiKey, _apiSecret)
        });
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol)
    {
        try
        {
            using var client = new BinanceClient();

            var result = await client.SpotApi.ExchangeData.GetPriceAsync(symbol);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Binance price request for {Symbol} failed: {Error}",
                    symbol,
                    result.Error?.Message);

                return 0m;
            }

            return result.Data.Price;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Binance price for {Symbol}",
                symbol);

            return 0m;
        }
    }

    public async Task<decimal> GetAvailableBalanceAsync(string quoteCurrency)
    {
        try
        {
            using var client = CreateClient();

            var result = await client.SpotApi.Account.GetAccountInfoAsync();

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Binance account request failed: {Error}",
                    result.Error?.Message);

                return 0m;
            }

            var balance = result.Data.Balances.FirstOrDefault(b =>
                string.Equals(
                    b.Asset,
                    quoteCurrency,
                    StringComparison.OrdinalIgnoreCase));

            return balance?.Available ?? 0m;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Binance balance for {Currency}",
                quoteCurrency);

            return 0m;
        }
    }

    public Task<string> PlaceMarketOrderAsync(
        string symbol,
        string side,
        decimal quantity) =>
        PlaceMarketOrderAsyncCoreAsync(
            symbol, side, quantity, allowWhenKillSwitchActive: false);

    public Task<string> PlaceClosingMarketOrderAsync(
        string symbol,
        string side,
        decimal quantity) =>
        PlaceMarketOrderAsyncCoreAsync(
            symbol, side, quantity, allowWhenKillSwitchActive: true);

    private async Task<string> PlaceMarketOrderAsyncCoreAsync(
        string symbol,
        string side,
        decimal quantity,
        bool allowWhenKillSwitchActive)
    {
        EnsureLiveOrdersAreEnabled();

        if (_safety.IsKillSwitchActive && !allowWhenKillSwitchActive)
            throw new InvalidOperationException(
                $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Order quantity must be greater than zero.");

        var orderSide = ParseOrderSide(side);

        // Optional pre-check for BUY orders on USDT pairs.
        // Binance still performs the authoritative balance and trade-rule checks.
        if (orderSide == OrderSide.Buy && symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            var currentPrice = await GetCurrentPriceAsync(symbol);

            if (currentPrice <= 0)
            {
                throw new InvalidOperationException(
                    $"Could not determine a valid current price for {symbol}.");
            }

            var estimatedCost = quantity * currentPrice;
            var availableUsdt = await GetAvailableBalanceAsync("USDT");

            if (availableUsdt < estimatedCost)
            {
                throw new InvalidOperationException(
                    $"Insufficient USDT balance. Estimated required: {estimatedCost:F8}; " +
                    $"available: {availableUsdt:F8}.");
            }
        }

        await _rateLimiter.WaitAsync();

        try
        {
            using var client = CreateClient();

            // Binance.Net v8.6.0 order is: symbol, side, type.
            var result = await client.SpotApi.Trading.PlaceOrderAsync(
                symbol,
                orderSide,
                SpotOrderType.Market,
                quantity: quantity);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Binance market order for {Symbol} was rejected: {Error}",
                    symbol,
                    result.Error?.Message);

                throw new InvalidOperationException(
                    $"Binance rejected the market order: {result.Error?.Message}");
            }

            var orderId = result.Data.Id.ToString();

            _orderSymbols[orderId] = symbol;

            _logger.LogInformation(
                "Placed Binance market order {OrderId} for {Symbol}",
                orderId,
                symbol);

            return orderId;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<string> PlaceLimitOrderAsync(
        string symbol,
        string side,
        decimal quantity,
        decimal price)
    {
        EnsureLiveOrdersAreEnabled();

        if (_safety.IsKillSwitchActive)
            throw new InvalidOperationException(
                $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Order quantity must be greater than zero.");

        if (price <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(price),
                "Limit price must be greater than zero.");

        var orderSide = ParseOrderSide(side);

        // Optional pre-check for BUY orders on USDT pairs.
        if (orderSide == OrderSide.Buy && symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
        {
            var estimatedCost = quantity * price;
            var availableUsdt = await GetAvailableBalanceAsync("USDT");

            if (availableUsdt < estimatedCost)
            {
                throw new InvalidOperationException(
                    $"Insufficient USDT balance. Required: {estimatedCost:F8}; " +
                    $"available: {availableUsdt:F8}.");
            }
        }

        await _rateLimiter.WaitAsync();

        try
        {
            using var client = CreateClient();

            // Binance.Net v8.6.0 order is: symbol, side, type.
            var result = await client.SpotApi.Trading.PlaceOrderAsync(
                symbol,
                orderSide,
                SpotOrderType.Limit,
                quantity: quantity,
                price: price,
                timeInForce: TimeInForce.GoodTillCanceled);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Binance limit order for {Symbol} was rejected: {Error}",
                    symbol,
                    result.Error?.Message);

                throw new InvalidOperationException(
                    $"Binance rejected the limit order: {result.Error?.Message}");
            }

            var orderId = result.Data.Id.ToString();

            // This was missing from your limit-order implementation.
            _orderSymbols[orderId] = symbol;

            _logger.LogInformation(
                "Placed Binance limit order {OrderId} for {Symbol}",
                orderId,
                symbol);

            return orderId;
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    public async Task<List<MarketData>> GetHistoricalCandlesAsync(
        string symbol,
        DateTime from,
        DateTime to,
        string timeframe = "1h")
    {
        try
        {
            using var client = new BinanceClient();

            var interval = timeframe.ToLowerInvariant() switch
            {
                "1m" => KlineInterval.OneMinute,
                "5m" => KlineInterval.FiveMinutes,
                "15m" => KlineInterval.FifteenMinutes,
                "1h" => KlineInterval.OneHour,
                "4h" => KlineInterval.FourHour,
                "1d" => KlineInterval.OneDay,
                _ => KlineInterval.OneHour
            };

            var result = await client.SpotApi.ExchangeData.GetKlinesAsync(
                symbol,
                interval,
                startTime: from,
                endTime: to,
                limit: 1000);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Binance candle request for {Symbol} failed: {Error}",
                    symbol,
                    result.Error?.Message);

                return new List<MarketData>();
            }

            return result.Data.Select(k => new MarketData
            {
                Symbol = symbol,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume,
                Timestamp = k.OpenTime
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve historical candles for {Symbol}",
                symbol);

            return new List<MarketData>();
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId)
    {
        try
        {
            if (!_orderSymbols.TryGetValue(orderId, out var symbol))
            {
                _logger.LogWarning(
                    "Cannot cancel Binance order {OrderId}: the symbol is unavailable.",
                    orderId);

                return false;
            }

            if (!long.TryParse(orderId, out var numericOrderId))
            {
                _logger.LogWarning(
                    "Cannot cancel Binance order {OrderId}: it is not numeric.",
                    orderId);

                return false;
            }

            using var client = CreateClient();

            var result = await client.SpotApi.Trading.CancelOrderAsync(
                symbol,
                orderId: numericOrderId);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to cancel Binance order {OrderId} for {Symbol}: {Error}",
                    orderId,
                    symbol,
                    result.Error?.Message);

                return false;
            }

            _orderSymbols.TryRemove(orderId, out _);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to cancel Binance order {OrderId}",
                orderId);

            return false;
        }
    }

    public async Task<object> GetOrderStatusAsync(string orderId)
    {
        try
        {
            if (!_orderSymbols.TryGetValue(orderId, out var symbol))
            {
                throw new InvalidOperationException(
                    $"Cannot retrieve order {orderId}: its Binance symbol is unavailable.");
            }

            if (!long.TryParse(orderId, out var numericOrderId))
            {
                throw new InvalidOperationException(
                    $"Order ID '{orderId}' is not a valid numeric Binance order ID.");
            }

            using var client = CreateClient();

            var result = await client.SpotApi.Trading.GetOrderAsync(
                symbol,
                orderId: numericOrderId);

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Binance could not retrieve order {orderId}: {result.Error?.Message}");
            }

            return result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to retrieve Binance status for order {OrderId}",
                orderId);

            throw;
        }
    }

    private static OrderSide ParseOrderSide(string side)
    {
        if (side.Equals("BUY", StringComparison.OrdinalIgnoreCase))
            return OrderSide.Buy;

        if (side.Equals("SELL", StringComparison.OrdinalIgnoreCase))
            return OrderSide.Sell;

        throw new ArgumentException(
            "Order side must be either BUY or SELL.",
            nameof(side));
    }

    private void EnsureLiveOrdersAreEnabled()
    {
        if (!_enableLiveOrders)
        {
            throw new InvalidOperationException(
                "Live Binance orders are disabled. " +
                "Set TradingBot:Binance:EnableLiveOrders to true only after explicit review.");
        }
    }
}