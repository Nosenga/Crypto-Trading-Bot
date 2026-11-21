using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TradingBot.Data;
using TradingBot.Models;
using TradingBot.Core.Interfaces; // <--- Add this namespace for IBrokerClient

namespace TradingBot.Services
{
    public class TradingBotService : ITradingBotService
    {
        private readonly AppDbContext _context;
        private readonly IMarketDataService _marketDataService;
        private readonly IBrokerClient _brokerClient; // <--- ADDED
        private readonly IConfiguration _configuration;
        private readonly ILogger<TradingBotService> _logger;
        private readonly ISafetyService _safety;

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

            quantity = await _brokerClient.NormalizeQuantityAsync(symbol, quantity);
            if (quantity <= 0)
                throw new InvalidOperationException(
                    $"Quantity too small after rounding for {symbol}.");

            // 1. Get price FROM THE BROKER instead of MarketDataService
            var currentPrice = await _brokerClient.GetCurrentPriceAsync(symbol);

            if (currentPrice == 0)
                throw new InvalidOperationException($"Unable to get price for {symbol}");

            var portfolio = await GetPortfolioAsync();
            var totalCost = quantity * currentPrice;

            // Validate balance for buy trades
            if (type == TradeType.Buy && portfolio.CurrentBalance < totalCost)
                throw new InvalidOperationException("Insufficient balance");

            // 2. Send the order to the Broker
            string side = type == TradeType.Buy ? "BUY" : "SELL";
            string? brokerOrderId = null;
            try
            {
                // Purely optional: this lets the "PaperBroker" generate a fake ID
                brokerOrderId = await _brokerClient.PlaceMarketOrderAsync(symbol, side, quantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Broker rejected the order for {Symbol}", symbol);
                throw new InvalidOperationException($"Broker rejected the order: {ex.Message}");
            }

            // 3. Only save to the DB if the Broker accepted the order
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
                Status = TradeStatus.Executed,
                ExecutedAt = DateTime.UtcNow,
                Confidence = confidence,
                SignalSource = signalSource,
                Notes = brokerOrderId // Store the exchange's Order ID here
            };

            // Update portfolio — Buy only. CurrentBalance is CASH ONLY;
            // realized P&L is tracked separately in RealizedPnL (see
            // CloseTradeAsync). This is the Phase 1 accounting model:
            //   CurrentBalance = free cash
            //   RealizedPnL    = sum of closed-trade profits
            //   TotalEquity    = CurrentBalance + market value of open longs
            portfolio.CurrentBalance -= totalCost;

            portfolio.LastUpdated = DateTime.UtcNow;
            _context.Trades.Add(trade);
            await _context.SaveChangesAsync();

            // Refresh after persisting the trade so its market value is included.
            await UpdatePortfolioAsync();

            _logger.LogInformation(
                "Executed {Type} trade for {Symbol}: {Quantity} @ {Price}",
                type, symbol, quantity, currentPrice);

            return trade;
        }

        public async Task<Trade?> CloseTradeAsync(int tradeId)
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

            // Phase 0: place the closing order BEFORE mutating the DB.
            // If the broker rejects it, the trade stays open and the DB stays
            // consistent with the exchange. In Paper mode this returns a fake id;
            // in Live mode it is the actual sell that frees the position.
            var closeSide = trade.Type == TradeType.Buy ? "SELL" : "BUY";
            string? closeOrderId;
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

            trade.ExitPrice = currentPrice;
            trade.ClosedAt = DateTime.UtcNow;
            // Phase 0: record the close order id so it can be reconciled later.
            trade.Notes = string.IsNullOrEmpty(trade.Notes)
                ? closeOrderId
                : $"{trade.Notes} | close={closeOrderId}";

            // Calculate P&L
            if (trade.Type == TradeType.Buy)
                trade.ProfitLoss = (currentPrice - trade.EntryPrice) * trade.Quantity;
            else // Sell order (Short)
                trade.ProfitLoss = (trade.EntryPrice - currentPrice) * trade.Quantity;

            trade.Status = TradeStatus.Closed;

            // Update portfolio (Phase 1 accounting model — single source of truth).
            // CurrentBalance is CASH ONLY. When we opened the long we did
            //   CurrentBalance -= (quantity * entryPrice)
            // so on close we return exactly that cost basis to cash:
            //   CurrentBalance += (quantity * entryPrice)
            // The profit/loss (exit - entry) * quantity is recorded ONLY in
            // RealizedPnL. Cash + realized P&L then reconcile correctly, and
            // TotalEquity (computed in UpdatePortfolioAsync) stays consistent.
            // Do NOT also add (currentPrice * quantity) to CurrentBalance —
            // that was the 308M double-count.
            var portfolio = await GetPortfolioAsync();
            portfolio.CurrentBalance += (trade.Quantity * trade.EntryPrice);
            portfolio.RealizedPnL += trade.ProfitLoss ?? 0;

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
        if (trade.Type == TradeType.Buy)
            unrealizedPnL += (currentPrice - trade.EntryPrice) * trade.Quantity;
        else
            unrealizedPnL += (trade.EntryPrice - currentPrice) * trade.Quantity;
    }

            // CurrentBalance excludes realized P&L, so include it separately.
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
            portfolio.TotalEquity = portfolio.CurrentBalance
                + portfolio.RealizedPnL
                + openMarketValue;
            portfolio.LastUpdated = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return portfolio;
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
                        catch (InvalidOperationException ex)
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
                            catch (InvalidOperationException ex)
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