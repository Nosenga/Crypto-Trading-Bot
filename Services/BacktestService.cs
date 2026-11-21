// Services/BacktestService.cs
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class BacktestService : IBacktestService
    {
        private const decimal FeeRate = 0.001m;

        private readonly AppDbContext _context;
        private readonly IMarketDataService _marketDataService;
        private readonly ILogger<BacktestService> _logger;

        public BacktestService(
            AppDbContext context,
            IMarketDataService marketDataService,
            ILogger<BacktestService> logger)
        {
            _context = context;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public async Task<BacktestResult> RunBacktestAsync(
            int strategyId, DateTime startDate, DateTime endDate,
            decimal initialCapital = 100000)
        {
            var strategy = await _context.Strategies.FindAsync(strategyId)
                ?? throw new ArgumentException("Strategy not found");

            var data = await _marketDataService
                .GetHistoricalDataAsync(strategy.Symbol, startDate, endDate);

            if (data.Count < 50)
            {
                throw new InvalidOperationException(
                    $"Not enough historical data for {strategy.Symbol} in this date range. " +
                    $"Found {data.Count} candles. Fetch real data first or adjust the dates.");
            }

            var result = new BacktestResult
            {
                StrategyName = strategy.Name,
                Symbol = strategy.Symbol,
                StartDate = startDate,
                EndDate = endDate,
                InitialCapital = initialCapital
            };

            var capital = initialCapital;
            var position = 0m;
            var entryPrice = 0m;
            var trades = new List<object>();
            var equityCurve = new List<decimal> { capital };
            var wins = 0;
            var losses = 0;
            var totalWin = 0m;
            var totalLoss = 0m;
            decimal largestWin = 0, largestLoss = 0;
            var peakEquity = capital;

            for (int i = 50; i < data.Count; i++)
            {
                var current = data[i];
                var signal = GetStrategySignal(strategy.Type, data, i);

                if (signal == "BUY" && position == 0)
                {
                    // Calculate position size based on risk
                    var riskAmount =
                        capital * (strategy.RiskPerTradePercent / 100);
                    var stopLossAmount = current.Close *
                        (strategy.StopLossPercent / 100);
                    position = stopLossAmount > 0
                        ? riskAmount / stopLossAmount
                        : 0;

                    entryPrice = current.Close;
                    var entryCost = position * entryPrice;
                    var entryFee = entryCost * FeeRate;
                    capital -= entryCost + entryFee;

                    trades.Add(new
                    {
                        Type = "BUY",
                        Date = current.Timestamp,
                        Price = current.Close,
                        Quantity = position,
                        Fee = entryFee
                    });
                }
                else if (signal == "SELL" && position > 0)
                {
                    var exitPrice = current.Close;
                    var exitValue = position * exitPrice;
                    var exitFee = exitValue * FeeRate;
                    var entryCost = position * entryPrice;
                    var entryFee = entryCost * FeeRate;
                    var netPnL = (exitValue - entryCost) - (entryFee + exitFee);
                    capital += exitValue - exitFee;

                    if (netPnL > 0) { wins++; totalWin += netPnL; }
                    else { losses++; totalLoss += Math.Abs(netPnL); }

                    largestWin = Math.Max(largestWin, netPnL);
                    largestLoss = Math.Min(largestLoss, netPnL);

                    trades.Add(new
                    {
                        Type = "SELL",
                        Date = current.Timestamp,
                        Price = exitPrice,
                        PnL = netPnL,
                        Fee = exitFee,
                        Return = (double)((exitPrice - entryPrice) /
                                          entryPrice * 100)
                    });

                    position = 0;
                    entryPrice = 0;
                }

                // Track equity
                var equity = capital + (position > 0
                    ? position * current.Close : 0);
                equityCurve.Add(equity);
                peakEquity = Math.Max(peakEquity, equity);
            }

            // Close any remaining position at last price
            if (position > 0)
            {
                var lastPrice = data.Last().Close;
                var exitValue = position * lastPrice;
                var exitFee = exitValue * FeeRate;
                capital += exitValue - exitFee;
            }

            result.FinalCapital = Math.Round(capital, 2);
            result.TotalReturn = Math.Round(capital - initialCapital, 2);
            result.TotalReturnPercent =
                Math.Round((double)(result.TotalReturn /
                                    initialCapital * 100), 2);

            var totalTrades = wins + losses;
            result.TotalTrades = totalTrades;
            result.WinningTrades = wins;
            result.LosingTrades = losses;
            result.WinRate = totalTrades > 0
                ? Math.Round((double)wins / totalTrades * 100, 1)
                : 0;
            result.LargestWin = Math.Round(largestWin, 2);
            result.LargestLoss = Math.Round(largestLoss, 2);
            result.AverageWin = wins > 0
                ? Math.Round(totalWin / wins, 2) : 0;
            result.AverageLoss = losses > 0
                ? Math.Round(totalLoss / losses, 2) : 0;

            // Calculate max drawdown
            var maxDrawdown = 0m;
            var peak = equityCurve[0];
            foreach (var eq in equityCurve)
            {
                peak = Math.Max(peak, eq);
                var drawdown = (peak - eq) / peak * 100;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }
            result.MaxDrawdownPercent = Math.Round((double)maxDrawdown, 2);

            // Annualized return
            var days = (endDate - startDate).TotalDays;
            if (days > 0)
            {
                result.AnnualizedReturn = Math.Round(
                    (Math.Pow(
                        (double)(result.FinalCapital / initialCapital),
                        365.0 / days) - 1) * 100, 2);
            }

            // Sharpe ratio (simplified)
            if (equityCurve.Count > 1)
            {
                var returns = new List<double>();
                for (int i = 1; i < equityCurve.Count; i++)
                {
                    if (equityCurve[i - 1] != 0)
                        returns.Add((double)(
                            (equityCurve[i] - equityCurve[i - 1]) /
                            equityCurve[i - 1]));
                }
                if (returns.Any())
                {
                    var avgReturn = returns.Average();
                    var stdDev = Math.Sqrt(
                        returns.Select(r =>
                            Math.Pow(r - avgReturn, 2)).Average());
                    result.SharpeRatio = stdDev > 0
                        ? Math.Round(avgReturn / stdDev * Math.Sqrt(252), 2)
                        : 0;
                }
            }

            result.EquityCurveJson = JsonSerializer.Serialize(equityCurve);
            result.TradeLogJson = JsonSerializer.Serialize(trades);

            _context.BacktestResults.Add(result);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Backtest completed for {Strategy}: {Return}%",
                strategy.Name, result.TotalReturnPercent);

            return result;
        }

        private string GetStrategySignal(
            StrategyType type, List<MarketData> data, int index)
        {
            var current = data[index];

            switch (type)
            {
                case StrategyType.MovingAverageCross:
                    if (index < 1) return "HOLD";
                    var prev = data[index - 1];
                    if (current.SMA20.HasValue && current.SMA50.HasValue &&
                        prev.SMA20.HasValue && prev.SMA50.HasValue)
                    {
                        if (prev.SMA20 <= prev.SMA50 &&
                            current.SMA20 > current.SMA50)
                            return "BUY";
                        if (prev.SMA20 >= prev.SMA50 &&
                            current.SMA20 < current.SMA50)
                            return "SELL";
                    }
                    break;

                case StrategyType.RSI:
                    if (current.RSI.HasValue)
                    {
                        if (current.RSI < 30) return "BUY";
                        if (current.RSI > 70) return "SELL";
                    }
                    break;

                case StrategyType.MACD:
                    if (index < 1) return "HOLD";
                    var prevMacd = data[index - 1];
                    if (current.MACDLine.HasValue &&
                        prevMacd.MACDLine.HasValue)
                    {
                        if (prevMacd.MACDLine < 0 &&
                            current.MACDLine > 0) return "BUY";
                        if (prevMacd.MACDLine > 0 &&
                            current.MACDLine < 0) return "SELL";
                    }
                    break;

                case StrategyType.BollingerBands:
                    if (current.BollingerLower.HasValue &&
                        current.Close <= current.BollingerLower.Value)
                        return "BUY";
                    if (current.BollingerUpper.HasValue &&
                        current.Close >= current.BollingerUpper.Value)
                        return "SELL";
                    break;
            }

            return "HOLD";
        }

        public async Task<List<BacktestResult>> GetBacktestHistoryAsync()
        {
            return await _context.BacktestResults
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        public async Task<BacktestResult?> GetBacktestResultAsync(int id)
        {
            return await _context.BacktestResults.FindAsync(id);
        }
    }
}