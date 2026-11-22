using System.Collections.Concurrent;
using System.Threading;
using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingBot.Core.Interfaces;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services;

public class BinanceBroker : IBrokerClient
{
    private const string BotOcoClientOrderIdPrefix = "TB-";

    private readonly ILogger<BinanceBroker> _logger;
    private readonly string _apiKey;
    private readonly string _apiSecret;
    private readonly bool _enableLiveOrders;
    private readonly bool _useTestnet;
    private readonly ISafetyService _safety;
    private readonly IPriceFeed _priceFeed;
    private readonly ConcurrentDictionary<string, (decimal StepSize, decimal MinNotional, decimal TickSize)> _filters =
        new(StringComparer.OrdinalIgnoreCase);

    // Binance requires the symbol when cancelling or querying a Spot order.
    // This mapping is only retained while the application is running.
    private readonly ConcurrentDictionary<string, string> _orderSymbols = new();

    // Serializes order submissions. This is not a complete API rate limiter,
    // but it prevents simultaneous order submissions from this broker instance.
    private readonly SemaphoreSlim _rateLimiter = new(1, 1);

    public BinanceBroker(
        ILogger<BinanceBroker> logger,
        IConfiguration configuration,
        ISafetyService safety,
        IPriceFeed priceFeed)
    {
        _logger = logger;
        _safety = safety;
        _priceFeed = priceFeed;

        _apiKey = configuration["TradingBot:Binance:ApiKey"]
            ?? throw new InvalidOperationException(
                "TradingBot:Binance:ApiKey is not configured.");

        _apiSecret = configuration["TradingBot:Binance:ApiSecret"]
            ?? throw new InvalidOperationException(
                "TradingBot:Binance:ApiSecret is not configured.");

        _enableLiveOrders = configuration.GetValue<bool>(
            "TradingBot:Binance:EnableLiveOrders");
        _useTestnet = configuration.GetValue<bool>(
            "TradingBot:Binance:UseTestnet", true);
    }

    private BinanceClient CreateClient()
    {
        return new BinanceClient(new BinanceClientOptions
        {
            ApiCredentials = new BinanceApiCredentials(_apiKey, _apiSecret),
            SpotApiOptions = new BinanceApiClientOptions
            {
                AutoTimestamp = true,
                BaseAddress = _useTestnet
                    ? "https://testnet.binance.vision"
                    : "https://api.binance.com"
            }
        });
    }

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

    public async Task<List<Trade>> GetOpenExchangePositionsAsync()
    {
        var trades = new List<Trade>();
        try
        {
            using var client = CreateClient();

            var ocoResult = await client.SpotApi.Trading.GetOpenOcoOrdersAsync();
            if (!ocoResult.Success)
            {
                _logger.LogError(
                    "Failed to fetch open OCO orders during reconciliation: {Error}",
                    ocoResult.Error?.Message);
                return trades;
            }

            var botOcoOrders = ocoResult.Data
                .Where(order => order.ListClientOrderId.StartsWith(
                    BotOcoClientOrderIdPrefix, StringComparison.Ordinal))
                .ToList();
            if (botOcoOrders.Count == 0)
                return trades;

            var openOrdersResult = await client.SpotApi.Trading.GetOpenOrdersAsync();
            if (!openOrdersResult.Success)
            {
                _logger.LogError(
                    "Failed to fetch open orders during reconciliation: {Error}",
                    openOrdersResult.Error?.Message);
                return trades;
            }

            foreach (var ocoOrder in botOcoOrders)
            {
                var orderLegs = openOrdersResult.Data
                    .Where(order => order.OrderListId == ocoOrder.Id &&
                        order.Side == OrderSide.Sell)
                    .ToList();
                if (orderLegs.Count == 0)
                {
                    _logger.LogWarning(
                        "Bot OCO list {ClientOrderId} has no open sell legs",
                        ocoOrder.ListClientOrderId);
                    continue;
                }

                var quantity = orderLegs.Min(order => order.QuantityRemaining);
                if (quantity <= 0)
                    continue;

                var currentPrice = await GetCurrentPriceAsync(ocoOrder.Symbol);
                if (currentPrice <= 0)
                {
                    _logger.LogError(
                        "Skipping reconciliation for {Symbol}: current price is unavailable",
                        ocoOrder.Symbol);
                    continue;
                }

                var stopLoss = orderLegs
                    .FirstOrDefault(order => order.StopPrice > 0)?.StopPrice;
                var takeProfit = orderLegs
                    .FirstOrDefault(order => order.StopPrice <= 0 && order.Price > 0)?.Price;

                trades.Add(new Trade
                {
                    Symbol = ocoOrder.Symbol,
                    Type = TradeType.Buy,
                    Quantity = quantity,
                    EntryPrice = currentPrice,
                    TotalValue = quantity * currentPrice,
                    StopLoss = stopLoss,
                    TakeProfit = takeProfit,
                    Status = TradeStatus.Executed,
                    ExecutedAt = ocoOrder.TransactionTime,
                    Notes = $"OCO_CLIENT:{ocoOrder.ListClientOrderId} | " +
                        $"Binance OCO {ocoOrder.Id}; entry price estimated at reconciliation"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reconcile open positions from Binance");
        }

        return trades;
    }

    public async Task<string> PlaceOcoOrderAsync(
        string symbol, string side, decimal quantity,
        decimal stopPrice, decimal limitPrice)
    {
        EnsureLiveOrdersAreEnabled();

        if (ParseOrderSide(side) != OrderSide.Sell)
            throw new InvalidOperationException(
                "OCO protection for a long Spot position must use the SELL side.");
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(quantity), "Order quantity must be greater than zero.");
        if (stopPrice <= 0 || limitPrice <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(stopPrice), "OCO stop and limit prices must be greater than zero.");

        await EnsureFiltersLoadedAsync(symbol);
        var roundedQuantity = RoundQuantity(symbol, quantity);
        if (roundedQuantity != quantity)
            throw new InvalidOperationException(
                $"OCO quantity for {symbol} is not aligned with the exchange step size; " +
                $"rounded-down quantity would be {roundedQuantity}.");

        (stopPrice, limitPrice) =
            await NormalizeOcoPricesAsync(symbol, stopPrice, limitPrice);

        var currentPrice = await GetCurrentPriceAsync(symbol);
        if (currentPrice <= 0)
            throw new InvalidOperationException(
                $"Could not determine a valid current price for {symbol}.");
        if (stopPrice >= currentPrice || limitPrice <= currentPrice ||
            stopPrice >= limitPrice)
        {
            throw new InvalidOperationException(
                $"For a SELL OCO on {symbol}, stop price must be below current price " +
                "and limit price must be above it.");
        }

        ValidateNotional(symbol, quantity, currentPrice);
        var stopLimitPrice = RoundPriceDown(symbol, stopPrice * 0.999m);
        if (stopLimitPrice <= 0 || stopLimitPrice >= stopPrice)
        {
            throw new InvalidOperationException(
                $"Could not derive a valid stop-limit price for {symbol}.");
        }

        var listClientOrderId =
            $"{BotOcoClientOrderIdPrefix}{Guid.NewGuid():N}";

        await _rateLimiter.WaitAsync();
        try
        {
            using var client = CreateClient();
            Exception placementError;
            var outcomeUnknown = true;

            try
            {
                var result = await client.SpotApi.Trading.PlaceOcoOrderAsync(
                    symbol,
                    OrderSide.Sell,
                    quantity,
                    price: limitPrice,
                    stopPrice: stopPrice,
                    stopLimitPrice: stopLimitPrice,
                    stopLimitTimeInForce: TimeInForce.GoodTillCanceled,
                    listClientOrderId: listClientOrderId);

                if (result.Success)
                {
                    if (result.Data.ListStatusType == ListStatusType.Done)
                    {
                        throw new OcoPlacementOutcomeUnknownException(
                            listClientOrderId,
                            $"Binance OCO {listClientOrderId} completed before the trade could be reconciled.");
                    }

                    return listClientOrderId;
                }

                placementError = new InvalidOperationException(
                    $"Binance rejected the OCO order: {result.Error?.Message}");
                outcomeUnknown = result.Error?.Code is null or -1006 or -1007;
            }
            catch (OcoPlacementOutcomeUnknownException)
            {
                throw;
            }
            catch (Exception ex)
            {
                placementError = ex;
            }

            var reconciledClientOrderId = await ReconcileOcoPlacementAsync(
                client, listClientOrderId, placementError);
            if (reconciledClientOrderId != null)
            {
                _logger.LogWarning(
                    "Recovered Binance OCO {ClientOrderId} after an uncertain placement response",
                    listClientOrderId);
                return reconciledClientOrderId;
            }

            if (outcomeUnknown)
            {
                throw new OcoPlacementOutcomeUnknownException(
                    listClientOrderId,
                    $"Could not confirm whether Binance accepted OCO {listClientOrderId}.",
                    placementError);
            }

            throw new InvalidOperationException(
                $"Binance rejected the OCO order: {placementError.Message}",
                placementError);
        }
        finally
        {
            _rateLimiter.Release();
        }
    }

    private async Task<string?> ReconcileOcoPlacementAsync(
        BinanceClient client, string listClientOrderId, Exception placementError)
    {
        try
        {
            var result = await client.SpotApi.Trading.GetOcoOrderAsync(
                listClientOrderId: listClientOrderId);
            if (result.Success)
            {
                if (result.Data.ListStatusType == ListStatusType.Done)
                {
                    throw new OcoPlacementOutcomeUnknownException(
                        listClientOrderId,
                        $"Binance OCO {listClientOrderId} completed before the trade could be reconciled.",
                        placementError);
                }

                return listClientOrderId;
            }

            if (result.Error?.Code == -2013)
                return null;

            throw new OcoPlacementOutcomeUnknownException(
                listClientOrderId,
                $"Could not determine the status of Binance OCO {listClientOrderId}: " +
                $"{result.Error?.Message}",
                placementError);
        }
        catch (OcoPlacementOutcomeUnknownException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OcoPlacementOutcomeUnknownException(
                listClientOrderId,
                $"Could not determine the status of Binance OCO {listClientOrderId}.",
                new AggregateException(placementError, ex));
        }
    }

    public Task SyncOpenTradesAsync(AppDbContext db, ILogger logger) =>
        Task.CompletedTask;

    public async Task<bool> CancelOcoAsync(
        string symbol, string listClientOrderId)
    {
        await _rateLimiter.WaitAsync();
        try
        {
            using var client = CreateClient();
            var result = long.TryParse(listClientOrderId, out var orderListId)
                ? await client.SpotApi.Trading.CancelOcoOrderAsync(
                    symbol, orderListId: orderListId)
                : await client.SpotApi.Trading.CancelOcoOrderAsync(
                    symbol, listClientOrderId: listClientOrderId);

            if (!result.Success)
            {
                _logger.LogWarning(
                    "Failed to cancel OCO {Id}: {Error}",
                    listClientOrderId, result.Error?.Message);
                return false;
            }

            _logger.LogInformation("Successfully cancelled OCO {Id}", listClientOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception cancelling OCO {Id}", listClientOrderId);
            return false;
        }
        finally
        {
            _rateLimiter.Release();
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
        OrderSide orderSide;
        try
        {
            EnsureLiveOrdersAreEnabled();

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    "Order quantity must be greater than zero.");

            orderSide = ParseOrderSide(side);

            await EnsureFiltersLoadedAsync(symbol);
            var roundedQuantity = RoundQuantity(symbol, quantity);
            if (allowWhenKillSwitchActive && roundedQuantity != quantity)
                throw new InvalidOperationException(
                    $"Order quantity for {symbol} is not aligned with the exchange step size; " +
                    $"rounded-down quantity would be {roundedQuantity}.");
            quantity = roundedQuantity;
            if (quantity <= 0)
                throw new InvalidOperationException(
                    $"Quantity too small after rounding for {symbol}.");

            var currentPrice = await GetCurrentPriceAsync(symbol);
            if (currentPrice <= 0)
                throw new InvalidOperationException(
                    $"Could not determine a valid current price for {symbol}.");

            ValidateNotional(symbol, quantity, currentPrice);

            // Optional pre-check for BUY orders on USDT pairs.
            // Binance still performs the authoritative balance and trade-rule checks.
            if (orderSide == OrderSide.Buy && symbol.EndsWith("USDT", StringComparison.OrdinalIgnoreCase))
            {
                var estimatedCost = quantity * currentPrice;
                var availableUsdt = await GetAvailableBalanceAsync("USDT");

                if (availableUsdt < estimatedCost)
                {
                    throw new InvalidOperationException(
                        $"Insufficient USDT balance. Estimated required: {estimatedCost:F8}; " +
                        $"available: {availableUsdt:F8}.");
                }
            }
        }
        catch (OrderRejectedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Nothing has been sent yet, so this is a definite rejection.
            throw new OrderRejectedException(ex.Message, ex);
        }

        await _rateLimiter.WaitAsync();

        try
        {
            if (_safety.IsKillSwitchActive && !allowWhenKillSwitchActive)
                throw new OrderRejectedException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");

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
                    "Binance market order for {Symbol} failed: {Error}",
                    symbol,
                    result.Error?.Message);

                var message = $"Binance rejected the market order: {result.Error?.Message}";

                // -1006 / -1007 (or no code at all) mean "execution status unknown".
                // Any other error code is a definite rejection.
                if (result.Error?.Code is null or -1006 or -1007)
                    throw new OrderOutcomeUnknownException(message);

                throw new OrderRejectedException(message);
            }

            var orderId = result.Data.Id.ToString();

            _orderSymbols[orderId] = symbol;

            _logger.LogInformation(
                "Placed Binance market order {OrderId} for {Symbol}",
                orderId,
                symbol);

            return orderId;
        }
        catch (Exception ex) when (ex is not OrderRejectedException &&
                                   ex is not OrderOutcomeUnknownException)
        {
            // Timeout, dropped connection, or anything unclassified once the request
            // may have left the process: assume the order MAY exist.
            throw new OrderOutcomeUnknownException(
                $"Could not confirm whether the {symbol} market order was sent: {ex.Message}", ex);
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

        await EnsureFiltersLoadedAsync(symbol);
        quantity = RoundQuantity(symbol, quantity);
        if (quantity <= 0)
            throw new InvalidOperationException(
                $"Quantity too small after rounding for {symbol}.");

        ValidateNotional(symbol, quantity, price);

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