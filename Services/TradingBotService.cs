using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TradingBot.Data;
using TradingBot.Models;
using TradingBot.Core.Interfaces; // <--- Add this namespace for IBrokerClient

namespace TradingBot.Services
{
    public class TradingBotService : ITradingBotService
    {
        private const string ShadowModeTradeMarker = "SHADOW_MODE";
        private static readonly SemaphoreSlim _tradeLock = new(1, 1);

        private readonly AppDbContext _context;
        private readonly IMarketDataService _marketDataService;
        private readonly IBrokerClient _brokerClient; // <--- ADDED
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingBotService> _logger;
        private readonly ISafetyService _safety;
        private const decimal FeeRate = 0.001m; // 0.1% Binance rate

        public TradingBotService(
            AppDbContext context,
            IMarketDataService marketDataService,
            IBrokerClient brokerClient, // <--- ADDED TO CONSTRUCTOR
            IConfiguration configuration,
            ILogger<TradingBotService> logger,
            ISafetyService safety)
        {
            _safety = safety;
            _context = context;
            _marketDataService = marketDataService;
            _brokerClient = brokerClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<Trade> ExecuteTradeAsync(
            string symbol, TradeType type, decimal quantity,
            string? strategy = null, decimal? stopLoss = null,
            decimal? takeProfit = null, double confidence = 80,
            string? signalSource = null)
        {
            await _tradeLock.WaitAsync();
            try
            {
                return await ExecuteTradeCoreAsync(
                    symbol, type, quantity, strategy, stopLoss, takeProfit,
                    confidence, signalSource);
            }
            finally
            {
                _tradeLock.Release();
            }
        }

        private static string Truncate(string s, int max = 450) =>
            s.Length <= max ? s : s[..max];

        private async Task<Trade> ExecuteTradeCoreAsync(
            string symbol, TradeType type, decimal quantity,
            string? strategy, decimal? stopLoss, decimal? takeProfit,
            double confidence, string? signalSource)
        {
            // Kill switch blocks all order paths, including manual trades.
            if (_safety.IsKillSwitchActive)
            {
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");
            }

            // Phase 1: long-only spot. Opening a short on Binance Spot is
            // not possible (you can only sell coins you own). Sell is
            // exit-only and lives in CloseTradeAsync. Reject any attempt to
            // open a Sell here rather than silently selling user coins.
            if (type == TradeType.Sell)
                throw new InvalidOperationException(
                    "Sell is exit-only (long-only spot). Use CloseTradeAsync to exit a long.");

            await UpdatePortfolioAsync();
            if (_safety.IsKillSwitchActive)
            {
                throw new InvalidOperationException(
                    $"Kill switch is active: {_safety.KillSwitchReason ?? "operator panic stop"}");
            }

            quantity = await _brokerClient.NormalizeQuantityAsync(symbol, quantity);
            if (quantity <= 0)
                throw new InvalidOperationException(
                    $"Quantity too small after rounding for {symbol}.");

            // 1. Get price FROM THE BROKER instead of MarketDataService
            var currentPrice = await _brokerClient.GetCurrentPriceAsync(symbol);

            if (currentPrice == 0)
                throw new InvalidOperationException($"Unable to get price for {symbol}");

            if (stopLoss is > 0 && takeProfit is > 0)
            {
                var normalizedPrices = await _brokerClient.NormalizeOcoPricesAsync(
                    symbol, stopLoss.Value, takeProfit.Value);
                stopLoss = normalizedPrices.StopPrice;
                takeProfit = normalizedPrices.LimitPrice;

                if (stopLoss.Value >= currentPrice ||
                    takeProfit.Value <= currentPrice)
                {
                    throw new InvalidOperationException(
                        $"OCO stop and take-profit prices are invalid for the current {symbol} price.");
                }
            }

            var portfolio = await GetPortfolioAsync();
            var totalCost = quantity * currentPrice;
            var entryFee = totalCost * FeeRate;

            // Validate balance for buy trades, including the entry fee.
            if (type == TradeType.Buy &&
                portfolio.CurrentBalance < totalCost + entryFee)
            {
                throw new InvalidOperationException(
                    "Insufficient balance (including fees)");
            }

            var maxPositionSize = _configuration.GetValue<decimal>(
                "TradingBot:MaxPositionSize", 1000m);
            if (totalCost > maxPositionSize)
            {
                throw new InvalidOperationException(
                    $"Trade cost of ${totalCost} exceeds MaxPositionSize of ${maxPositionSize}.");
            }

            var maxOpenPositions = _configuration.GetValue<int>(
                "TradingBot:MaxOpenPositions", 10);
            var openCount = await _context.Trades.CountAsync(
                trade => trade.Status == TradeStatus.Executed ||
                         trade.Status == TradeStatus.Pending);
            if (openCount >= maxOpenPositions)
            {
                throw new InvalidOperationException(
                    $"Cannot open trade. Max open positions ({maxOpenPositions}) reached.");
            }

            // Persist a pending record before sending an order so a process
            // failure cannot leave an exchange position with no local record.
            var isShadowTrade = !_safety.IsArmed;
            var trade = new Trade
            {
                Symbol = symbol,
                Type = type,
                EntryPrice = currentPrice,
                Quantity = quantity,
                TotalValue = totalCost,
                StopLoss = stopLoss,
                TakeProfit = takeProfit,
                Strategy = strategy,
                Status = TradeStatus.Pending,
                ExecutedAt = DateTime.UtcNow,
                Confidence = confidence,
                SignalSource = signalSource,
                Notes = isShadowTrade ? ShadowModeTradeMarker : "PENDING"
            };

            _context.Trades.Add(trade);
            await _context.SaveChangesAsync();

            if (isShadowTrade)
            {
                _logger.LogInformation(
                    "[Shadow Mode] Would have bought {Quantity} {Symbol}",
                    quantity, symbol);
            }
            else
            {
                try
                {
                    var brokerOrderId = await _brokerClient.PlaceMarketOrderAsync(
                        symbol, "BUY", quantity);
                    trade.Notes = brokerOrderId;
                }
                catch (OrderRejectedException ex)
                {
                    // Definite rejection: nothing reached the exchange, so there is
                    // nothing to reconcile. Cancel the row so it neither holds an
                    // open-position slot nor lingers as "pending".
                    trade.Status = TradeStatus.Cancelled;
                    trade.ExecutedAt = null;
                    trade.Notes = Truncate($"REJECTED: {ex.Message}");
                    await _context.SaveChangesAsync();
                    _logger.LogWarning(
                        ex,
                        "Broker rejected the order for trade #{TradeId} {Symbol}",
                        trade.Id,
                        symbol);
                    throw new InvalidOperationException(
                        $"Broker rejected the order: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    // Outcome unknown (timeout, lost response, or anything unclassified).
                    // The order may exist on the exchange, so the row stays Pending.
                    trade.Notes = Truncate($"PENDING_RECONCILIATION: {ex.Message}");
                    await _context.SaveChangesAsync();
                    _logger.LogCritical(
                        ex,
                        "Could not confirm market order outcome for pending trade #{TradeId} {Symbol}",
                        trade.Id,
                        symbol);
                    throw new InvalidOperationException(
                        "The broker order outcome is uncertain. The trade remains pending and " +
                        "must be reconciled before retrying.",
                        ex);
                }
            }

            trade.Fees = entryFee;
            trade.Status = TradeStatus.Executed;

            // CurrentBalance is actual cash; realized P&L is reported
            // separately but is already reflected in cash after a close.
            portfolio.CurrentBalance -= totalCost + entryFee;

            portfolio.LastUpdated = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (!isShadowTrade && stopLoss is > 0 && takeProfit is > 0)
            {
                try
                {
                    var ocoClientOrderId = await _brokerClient.PlaceOcoOrderAsync(
                        symbol, "SELL", quantity, stopLoss.Value, takeProfit.Value);
                    trade.Notes = $"{trade.Notes} | OCO_CLIENT:{ocoClientOrderId}";
                    await _context.SaveChangesAsync();
                }
                catch (OcoPlacementOutcomeUnknownException ex)
                {
                    trade.Notes = $"{trade.Notes} | OCO_CLIENT:{ex.ClientOrderId}";
                    await _context.SaveChangesAsync();

                    _logger.LogCritical(
                        ex,
                        "OCO placement status is uncertain for {Symbol}; leaving trade #{TradeId} open",
                        symbol,
                        trade.Id);
                    throw new InvalidOperationException(
                        "OCO placement status is uncertain. The position remains open; " +
                        "check Binance before taking further action.",
                        ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "OCO placement failed for {Symbol}; attempting an emergency close",
                        symbol);

                    try
                    {
                        var closedTrade = await CloseTradeCoreAsync(trade.Id);
                        if (closedTrade == null)
                        {
                            throw new InvalidOperationException(
                                $"Trade #{trade.Id} could not be closed after OCO placement failed.");
                        }

                        await UpdatePortfolioAsync();
                    }
                    catch (Exception closeException)
                    {
                        trade.Notes =
                            $"{trade.Notes} | OCO_FAILED; emergency close also failed";
                        await _context.SaveChangesAsync();
                        _logger.LogCritical(
                            closeException,
                            "Emergency close failed for trade #{TradeId} {Symbol} after OCO failure",
                            trade.Id,
                            symbol);
                        throw new InvalidOperationException(
                            "Could not secure the position with OCO, and the emergency close failed. " +
                            "The position remains open and requires immediate review.",
                            new AggregateException(ex, closeException));
                    }

                    throw new InvalidOperationException(
                        "Could not secure the position with OCO. The position was closed.",
                        ex);
                }
            }

            // Refresh after OCO placement so the trade and its protection are
            // already persisted before the portfolio refresh does more I/O.
            await UpdatePortfolioAsync();

            _logger.LogInformation(
                "Executed {Type} trade for {Symbol}: {Quantity} @ {Price}",
                type, symbol, quantity, currentPrice);

            return trade;
        }

        public async Task<Trade?> CloseTradeAsync(int tradeId)
        {
            await _tradeLock.WaitAsync();
            try
            {
                return await CloseTradeCoreAsync(tradeId);
            }
            finally
            {
                _tradeLock.Release();
            }
        }

        private async Task<Trade?> CloseTradeCoreAsync(int tradeId)
        {
            var trade = await _context.Trades.FindAsync(tradeId);
            if (trade == null || trade.Status != TradeStatus.Executed)
                return null;

            // Phase 0: explicit kill-switch policy.
            // Closes are ALLOWED while the kill switch is active on purpose —
            // the point of a panic stop is to let the operator flatten positions,
            // not trap them. Only *opens* are blocked (see ExecuteTradeAsync).
            // If you ever want closes blocked too, add an explicit forceClose
            // parameter and gate on it — do NOT change this silently.

            // Get current price FROM THE BROKER
            var currentPrice = await _brokerClient.GetCurrentPriceAsync(trade.Symbol);

            // Phase 0: price-0 guard. A network blip must never close a trade at 0.
            if (currentPrice <= 0)
            {
                _logger.LogWarning(
                    "CloseTradeAsync skipped for #{Id} {Symbol}: invalid price {Price}",
                    trade.Id, trade.Symbol, currentPrice);
                return null;
            }

            var closeSide = trade.Type == TradeType.Buy ? "SELL" : "BUY";
            var ocoMatch = System.Text.RegularExpressions.Regex.Match(
                trade.Notes ?? string.Empty,
                @"OCO_CLIENT:([^| ]+)|OCO:\s*(\d+)");
            if (ocoMatch.Success)
            {
                var ocoId = ocoMatch.Groups[1].Success
                    ? ocoMatch.Groups[1].Value
                    : ocoMatch.Groups[2].Value;
                var cancelled = await _brokerClient.CancelOcoAsync(
                    trade.Symbol, ocoId);
                if (!cancelled)
                {
                    _logger.LogWarning(
                        "Failed to cancel OCO {Id} for trade #{TradeId}. Proceeding may fail.",
                        ocoId, trade.Id);
                }
            }

            string? closeOrderId;
            var isShadowTrade = trade.Notes?.StartsWith(
                ShadowModeTradeMarker, StringComparison.Ordinal) == true;
            if (isShadowTrade)
            {
                closeOrderId = ShadowModeTradeMarker;
                _logger.LogInformation(
                    "[Shadow Mode] Would have sold {Quantity} {Symbol}",
                    trade.Quantity, trade.Symbol);
            }
            else
            {
                try
                {
                    closeOrderId = await _brokerClient.PlaceClosingMarketOrderAsync(
                        trade.Symbol, closeSide, trade.Quantity);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "CloseTradeAsync failed to place broker order for #{Id} {Symbol}",
                        trade.Id, trade.Symbol);
                    throw new InvalidOperationException(
                        $"Broker rejected the close order: {ex.Message}", ex);
                }
            }

            trade.ExitPrice = currentPrice;
            trade.ClosedAt = DateTime.UtcNow;
            // Phase 0: record the close order id so it can be reconciled later.
            trade.Notes = string.IsNullOrEmpty(trade.Notes)
                ? closeOrderId
                : $"{trade.Notes} | close={closeOrderId}";

            var exitValue = trade.Quantity * currentPrice;
            var exitFee = exitValue * FeeRate;
            var grossPnL = trade.Type == TradeType.Buy
                ? (currentPrice - trade.EntryPrice) * trade.Quantity
                : (trade.EntryPrice - currentPrice) * trade.Quantity;

            trade.ProfitLoss = grossPnL - trade.Fees - exitFee;
            trade.Fees += exitFee;

            trade.Status = TradeStatus.Closed;

            // Return actual sale proceeds to cash. RealizedPnL is a reporting
            // metric and must not be added to TotalEquity a second time.
            var portfolio = await GetPortfolioAsync();
            portfolio.CurrentBalance += exitValue - exitFee;
            portfolio.RealizedPnL += trade.ProfitLoss.Value;

            if (trade.ProfitLoss > 0) portfolio.WinningTrades++;
            else if (trade.ProfitLoss < 0) portfolio.LosingTrades++;
            portfolio.TotalTrades++;

            portfolio.LastUpdated = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(trade.Strategy))
            {
                var owningStrategy = await _context.Strategies
                    .FirstOrDefaultAsync(s => s.Name == trade.Strategy);
                if (owningStrategy != null)
                {
                    owningStrategy.TotalTrades++;
                    if (trade.ProfitLoss > 0) owningStrategy.WinningTrades++;
                    else if (trade.ProfitLoss < 0) owningStrategy.LosingTrades++;
                    owningStrategy.TotalProfitLoss += trade.ProfitLoss ?? 0;
                    owningStrategy.LastExecutedAt = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Closed trade #{Id} for {Symbol}: P&L = {PnL:C}",
                tradeId, trade.Symbol, trade.ProfitLoss);

            return trade;
        }

        public async Task<List<Trade>> GetOpenTradesAsync()
        {
            // StopLossHit / TakeProfitHit trades are excluded here because
            // they are closed, and included in closed-trade dashboard stats.
            return await _context.Trades
                .Where(t => t.Status == TradeStatus.Executed)
                .OrderByDescending(t => t.ExecutedAt)
                .ToListAsync();
        }

        public async Task<List<Trade>> GetTradeHistoryAsync(int count = 100)
        {
            return await _context.Trades
                .OrderByDescending(t => t.CreatedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Portfolio> GetPortfolioAsync()
        {
            var portfolio = await _context.Portfolio.FirstOrDefaultAsync();
            if (portfolio == null)
            {
                portfolio = new Portfolio();
                _context.Portfolio.Add(portfolio);
                await _context.SaveChangesAsync();
            }
            return portfolio;
        }

        public async Task<Portfolio> UpdatePortfolioAsync()
        {
            var portfolio = await GetPortfolioAsync();
            var openTrades = await GetOpenTradesAsync();

            decimal unrealizedPnL = 0;
            var symbols = openTrades.Select(t => t.Symbol).Distinct().ToList();
            
            // Use Broker for prices to ensure accuracy
            var prices = new Dictionary<string, decimal>();
            foreach (var sym in symbols)
    {
        try
        {
            var p = await _brokerClient.GetCurrentPriceAsync(sym);
            if (p > 0) prices[sym] = p;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Portfolio refresh: price unavailable for {Symbol}", sym);
        }
    }

    foreach (var trade in openTrades)
    {
        var currentPrice = prices.GetValueOrDefault(trade.Symbol, 0);
        if (currentPrice <= 0)
        {
            _logger.LogWarning(
                "Skipping P&L for trade #{Id} {Symbol}: invalid price {Price}",
                trade.Id, trade.Symbol, currentPrice);
            continue;
        }
        var grossPnL = trade.Type == TradeType.Buy
            ? (currentPrice - trade.EntryPrice) * trade.Quantity
            : (trade.EntryPrice - currentPrice) * trade.Quantity;
        unrealizedPnL += grossPnL - trade.Fees;
    }

            decimal openMarketValue = 0m;
            foreach (var trade in openTrades)
            {
                if (trade.Type != TradeType.Buy)
                    continue;

                var currentPrice = prices.GetValueOrDefault(trade.Symbol, 0);
                if (currentPrice > 0)
                    openMarketValue += trade.Quantity * currentPrice;
            }

            portfolio.UnrealizedPnL = unrealizedPnL;
            portfolio.TotalEquity = portfolio.CurrentBalance + openMarketValue;
            portfolio.LastUpdated = DateTime.UtcNow;

            CheckRiskLimits(portfolio);

            await _context.SaveChangesAsync();
            return portfolio;
        }

        private void CheckRiskLimits(Portfolio portfolio)
        {
            var now = DateTime.UtcNow;
            if (portfolio.LastRiskCheckDate.Date < now.Date)
                portfolio.StartOfDayEquity = portfolio.TotalEquity;

            portfolio.LastRiskCheckDate = now;
            _safety.UpdateHighWaterMark(portfolio);

            var dailyLossLimitPercent = _configuration.GetValue<decimal>(
                "TradingBot:RiskManagement:DailyLossLimitPercent", 3m);
            var maxDrawdownLimitPercent = _configuration.GetValue<decimal>(
                "TradingBot:RiskManagement:MaxDrawdownLimitPercent", 10m);

            if (portfolio.StartOfDayEquity > 0)
            {
                var lossPercent =
                    (portfolio.StartOfDayEquity - portfolio.TotalEquity) /
                    portfolio.StartOfDayEquity * 100;
                if (lossPercent >= dailyLossLimitPercent &&
                    !_safety.IsKillSwitchActive)
                {
                    _safety.ActivateKillSwitch("Daily Loss Limit Hit");
                    _logger.LogCritical(
                        "Kill switch activated: Daily Loss Limit Hit");
                }
            }

            var drawdownThreshold = portfolio.HighWaterMark *
                (1 - maxDrawdownLimitPercent / 100);
            if (portfolio.HighWaterMark > 0 &&
                portfolio.TotalEquity < drawdownThreshold &&
                !_safety.IsKillSwitchActive)
            {
                _safety.ActivateKillSwitch("Max Drawdown Limit Hit");
                _logger.LogCritical(
                    "Kill switch activated: Max Drawdown Limit Hit");
            }
        }

        public async Task<bool> CheckStopLossAndTakeProfitAsync()
{
    var openTrades = await GetOpenTradesAsync();
    var anyClosed = false;

    foreach (var trade in openTrades)
    {
        if (!trade.StopLoss.HasValue && !trade.TakeProfit.HasValue)
            continue;

        decimal currentPrice;
        try
        {
            currentPrice = await _brokerClient.GetCurrentPriceAsync(trade.Symbol);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "SL/TP check skipped for trade #{Id} {Symbol}: price unavailable",
                trade.Id, trade.Symbol);
            continue;
        }

        if (currentPrice <= 0)
        {
            _logger.LogWarning(
                "SL/TP check skipped for trade #{Id} {Symbol}: invalid price {Price}",
                trade.Id, trade.Symbol, currentPrice);
            continue;
        }

        // --- LOGIC FOR BUY POSITIONS ---
        if (trade.Type == TradeType.Buy)
        {
            if (trade.StopLoss.HasValue && currentPrice <= trade.StopLoss.Value)
            {
                var closed = await CloseTradeAsync(trade.Id);
                if (closed != null)
                {
                    closed.Status = TradeStatus.StopLossHit;
                    anyClosed = true;
                }
            }
            else if (trade.TakeProfit.HasValue && currentPrice >= trade.TakeProfit.Value)
            {
                var closed = await CloseTradeAsync(trade.Id);
                if (closed != null)
                {
                    closed.Status = TradeStatus.TakeProfitHit;
                    anyClosed = true;
                }
            }
        }
        // Phase 1: long-only spot. Short SL/TP logic removed.
        // If a Sell-type trade ever exists in the DB (legacy data), it will
        // be ignored here — Phase 1 DB wipe removes old shorts anyway.
    }

    if (anyClosed) await _context.SaveChangesAsync();
    return anyClosed;
}

        public async Task<int> RunAutoTradingCycleAsync()
        {
            if (_safety.IsKillSwitchActive)
            {
                _logger.LogInformation(
                    "Auto-trading skipped: kill switch active ({Reason})",
                    _safety.KillSwitchReason);
                return 0;
            }

            var actionsTaken = 0;
            var maxOpenPositions = _configuration
                .GetValue<int>("TradingBot:MaxOpenPositions", 10);

            var activeStrategies = await _context.Strategies
                .Where(s => s.Status == StrategyStatus.Active)
                .ToListAsync();

            if (!activeStrategies.Any()) return 0;

            var pendingSignals = await _context.Signals
                .Where(s => !s.IsExecuted &&
                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(s => s.GeneratedAt)
                .ToListAsync();

            if (!pendingSignals.Any()) return 0;

            // Get the portfolio ONCE before the loops so we can use it for both BUY and SELL
            var portfolio = await GetPortfolioAsync();

            foreach (var strategy in activeStrategies)
            {
                var source = SignalSourceFor(strategy.Type);
                if (source == null) continue;

                // Only act on the freshest pending signal per symbol for this strategy.
                var latestPerSymbol = pendingSignals
                    .Where(s => s.Source == source)
                    .GroupBy(s => s.Symbol)
                    .Select(g => g.OrderByDescending(s => s.GeneratedAt).First());

                foreach (var signal in latestPerSymbol)
                {
                    var openTrade = await _context.Trades.FirstOrDefaultAsync(t =>
                        t.Strategy == strategy.Name &&
                        t.Symbol == signal.Symbol &&
                        t.Status == TradeStatus.Executed);

                    var isBuySignal = signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy;
                    var isSellSignal = signal.Type == SignalType.Sell || signal.Type == SignalType.StrongSell;

                    // Skip duplicate buys, but allow sell signals to reach the exit path.
                    if (openTrade != null && isBuySignal)
                    {
                        signal.IsExecuted = true;
                        continue;
                    }

                    // --- BUY LOGIC ---
                    if (isBuySignal)
                    {
                        var openCount = await _context.Trades.CountAsync(t => t.Status == TradeStatus.Executed);
                        if (openCount >= maxOpenPositions)
                        {
                            signal.IsExecuted = true;
                            continue;
                        }

                        var maxPosSize = _configuration.GetValue<decimal>("TradingBot:MaxPositionSize", 500m);

                        var riskAmount = portfolio.CurrentBalance * (strategy.RiskPerTradePercent / 100);
                        var stopLossAmount = signal.Price * (strategy.StopLossPercent / 100);
                        var rawQuantity = stopLossAmount > 0 ? riskAmount / stopLossAmount : 0;

                        var rawCost = rawQuantity * signal.Price;

                        decimal quantity = rawQuantity;
                        if (rawCost > maxPosSize)
                        {
                            quantity = maxPosSize / signal.Price;
                        }

                        if (quantity <= 0)
                        {
                            signal.IsExecuted = true;
                            continue;
                        }

                        var cost = quantity * signal.Price;

                        // //--- SAFETY CHECK FOR BUY ---
                        // var maxPositionSize = _configuration.GetValue<decimal>("TradingBot:MaxPositionSize", 500m);
                        // if (cost > maxPositionSize)
                        // {
                        //     _logger.LogWarning("BUY Trade cost of {Cost} exceeds MaxPositionSize of {Max}. Skipping trade.", cost, maxPositionSize);
                        //     signal.IsExecuted = true;
                        //     continue;
                        // }
                        // //----------------------------

                        if (cost > portfolio.CurrentBalance)
                        {
                            signal.IsExecuted = true;
                            continue;
                        }

                        var stopLossPrice = signal.Price * (1 - strategy.StopLossPercent / 100);
                        var takeProfitPrice = signal.Price * (1 + strategy.TakeProfitPercent / 100);

                        try
                        {
                            await ExecuteTradeAsync(
                                signal.Symbol, TradeType.Buy, quantity,
                                strategy.Name, stopLossPrice, takeProfitPrice,
                                signal.Confidence, signal.Source);
                            actionsTaken++;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "Auto-trade buy skipped for {Symbol} ({Strategy})",
                                signal.Symbol, strategy.Name);
                        }
                    }
                    // --- SELL SIGNAL = EXIT-ONLY (long-only spot) ---
                    else if (isSellSignal)
                    {
                        if (openTrade == null || openTrade.Type != TradeType.Buy)
                        {
                            _logger.LogInformation(
                                "Sell signal for {Symbol} ({Strategy}) ignored: no open long to close (long-only spot)",
                                signal.Symbol, strategy.Name);
                        }
                        else
                        {
                            try
                            {
                                var closedTrade = await CloseTradeAsync(openTrade.Id);
                                if (closedTrade != null)
                                    actionsTaken++;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex,
                                    "Auto-trade exit skipped for {Symbol} ({Strategy})",
                                    signal.Symbol, strategy.Name);
                            }
                        }
                    }

                    signal.IsExecuted = true;
                }
            }

            await _context.SaveChangesAsync();
            return actionsTaken;
        }

        private static string? SignalSourceFor(StrategyType type) => type switch
        {
            StrategyType.MovingAverageCross => "MA Crossover",
            StrategyType.RSI => "RSI",
            StrategyType.MACD => "MACD",
            StrategyType.BollingerBands => "Bollinger Bands",
            _ => null
        };

        public async Task<Dictionary<string, object>> GetDashboardStatsAsync()
        {
            var portfolio = await UpdatePortfolioAsync();
            var openTrades = await GetOpenTradesAsync();
            // Phase 1: StopLossHit / TakeProfitHit trades are closed too.
            // Excluding them understated closed-trade stats and made SL/TP
            // exits vanish from win-rate and P&L totals.
            var closedStatuses = new[]
            {
                TradeStatus.Closed,
                TradeStatus.StopLossHit,
                TradeStatus.TakeProfitHit
            };
            var recentTrades = await _context.Trades
                .Where(t => closedStatuses.Contains(t.Status))
                .OrderByDescending(t => t.ClosedAt)
                .Take(20)
                .ToListAsync();

            var totalPnL = recentTrades.Sum(t => t.ProfitLoss ?? 0);
            var winRate = recentTrades.Count > 0
                ? (double)recentTrades.Count(t => t.ProfitLoss > 0) / recentTrades.Count * 100
                : 0;

            return new Dictionary<string, object>
            {
                ["TotalEquity"] = portfolio.TotalEquity,
                ["CurrentBalance"] = portfolio.CurrentBalance,
                ["UnrealizedPnL"] = portfolio.UnrealizedPnL,
                ["RealizedPnL"] = portfolio.RealizedPnL,
                ["TotalPnL"] = portfolio.TotalPnL,
                ["ReturnPercent"] = portfolio.ReturnPercent,
                ["OpenPositions"] = openTrades.Count,
                ["WinRate"] = winRate,
                ["TotalTrades"] = portfolio.TotalTrades,
                ["ActiveStrategies"] = await _context.Strategies
                    .CountAsync(s => s.Status == StrategyStatus.Active),
                ["RecentTrades"] = recentTrades,
                ["OpenTradeList"] = openTrades
            };
        }
    }
}