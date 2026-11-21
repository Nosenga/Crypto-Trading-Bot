using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class SignalService : ISignalService
    {
        private readonly AppDbContext _context;
        private readonly IMarketDataService _marketDataService;
        private readonly ILogger<SignalService> _logger;

        public SignalService(
            AppDbContext context,
            IMarketDataService marketDataService,
            ILogger<SignalService> logger)
        {
            _context = context;
            _marketDataService = marketDataService;
            _logger = logger;
        }

        public async Task<List<Signal>> GenerateSignalsAsync(string symbol)
        {
            var signals = new List<Signal>();

            // Phase 2c: Only fetch closed candles
            var data = await _context.MarketData
                .Where(m => m.Symbol == symbol && m.IsClosed == true) // <--- The CRITICAL FIX
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .ToListAsync();

            if (data.Count < 50) return signals;

            data.Reverse();
            var latest = data.Last();

            // 1. Moving Average Crossover Signal
            var maSignal = AnalyzeMASignal(data, latest);
            if (maSignal != null) signals.Add(maSignal);

            // 2. RSI Signal
            var rsiSignal = AnalyzeRSISignal(latest);
            if (rsiSignal != null) signals.Add(rsiSignal);

            // 3. MACD Signal
            var macdSignal = AnalyzeMACDSignal(data, latest);
            if (macdSignal != null) signals.Add(macdSignal);

            // 4. Bollinger Bands Signal
            var bbSignal = AnalyzeBollingerSignal(latest);
            if (bbSignal != null) signals.Add(bbSignal);

            // Save signals
            if (signals.Any())
            {
                _context.Signals.AddRange(signals);
                await _context.SaveChangesAsync();
                _logger.LogInformation(
                    "Generated {Count} signals for {Symbol}",
                    signals.Count, symbol);
            }

            return signals;
        }

        private Signal? AnalyzeMASignal(List<MarketData> data, MarketData latest)
        {
            if (data.Count < 2) return null;

            var previous = data[^2];

            if (latest.SMA20.HasValue && latest.SMA50.HasValue &&
                previous.SMA20.HasValue && previous.SMA50.HasValue)
            {
                // Golden Cross
                if (previous.SMA20 <= previous.SMA50 &&
                    latest.SMA20 > latest.SMA50)
                {
                    return new Signal
                    {
                        Symbol = latest.Symbol,
                        Type = SignalType.Buy,
                        Price = latest.Close,
                        Confidence = 72,
                        Source = "MA Crossover",
                        Reason = "Golden Cross: SMA20 crossed above SMA50"
                    };
                }

                // Death Cross
                if (previous.SMA20 >= previous.SMA50 &&
                    latest.SMA20 < latest.SMA50)
                {
                    return new Signal
                    {
                        Symbol = latest.Symbol,
                        Type = SignalType.Sell,
                        Price = latest.Close,
                        Confidence = 70,
                        Source = "MA Crossover",
                        Reason = "Death Cross: SMA20 crossed below SMA50"
                    };
                }
            }

            return null;
        }

        private Signal? AnalyzeRSISignal(MarketData latest)
        {
            if (!latest.RSI.HasValue) return null;

            // Convert the decimal RSI to double so we can do math easily
            double currentRSI = (double)latest.RSI.Value;

            // Hardcode fallback values (You can change these later via JSON parsing)
            double oversoldLevel = 30;
            double overboughtLevel = 70;

            if (currentRSI < oversoldLevel)
            {
                // Confidence formula: 75 + (30 - currentRSI)
                double confidence = Math.Min(100, 75 + (oversoldLevel - currentRSI));

                return new Signal
                {
                    Symbol = latest.Symbol,
                    Type = SignalType.StrongBuy,
                    Price = latest.Close,
                    Confidence = confidence,
                    Source = "RSI",
                    Reason = $"RSI oversold at {currentRSI:F1}"
                };
            }

            if (currentRSI > overboughtLevel)
            {
                // Confidence formula: 75 + (currentRSI - 70)
                double confidence = Math.Min(100, 75 + (currentRSI - overboughtLevel));

                return new Signal
                {
                    Symbol = latest.Symbol,
                    Type = SignalType.StrongSell,
                    Price = latest.Close,
                    Confidence = confidence,
                    Source = "RSI",
                    Reason = $"RSI overbought at {currentRSI:F1}"
                };
            }

            return null;
        }

        private Signal? AnalyzeMACDSignal(
            List<MarketData> data, MarketData latest)
        {
            if (data.Count < 2) return null;
            var previous = data[^2];

            if (latest.MACDLine.HasValue && previous.MACDLine.HasValue)
            {
                // MACD crossing above zero
                if (previous.MACDLine < 0 && latest.MACDLine > 0)
                {
                    return new Signal
                    {
                        Symbol = latest.Symbol,
                        Type = SignalType.Buy,
                        Price = latest.Close,
                        Confidence = 68,
                        Source = "MACD",
                        Reason = "MACD crossed above zero line"
                    };
                }

                // MACD crossing below zero
                if (previous.MACDLine > 0 && latest.MACDLine < 0)
                {
                    return new Signal
                    {
                        Symbol = latest.Symbol,
                        Type = SignalType.Sell,
                        Price = latest.Close,
                        Confidence = 68,
                        Source = "MACD",
                        Reason = "MACD crossed below zero line"
                    };
                }
            }

            return null;
        }

        private Signal? AnalyzeBollingerSignal(MarketData latest)
        {
            if (!latest.BollingerUpper.HasValue ||
                !latest.BollingerLower.HasValue) return null;

            if (latest.Close <= latest.BollingerLower.Value)
            {
                return new Signal
                {
                    Symbol = latest.Symbol,
                    Type = SignalType.Buy,
                    Price = latest.Close,
                    Confidence = 65,
                    Source = "Bollinger Bands",
                    Reason = "Price touched lower Bollinger Band"
                };
            }

            if (latest.Close >= latest.BollingerUpper.Value)
            {
                return new Signal
                {
                    Symbol = latest.Symbol,
                    Type = SignalType.Sell,
                    Price = latest.Close,
                    Confidence = 65,
                    Source = "Bollinger Bands",
                    Reason = "Price touched upper Bollinger Band"
                };
            }

            return null;
        }

        public async Task<List<Signal>> GetActiveSignalsAsync()
        {
            return await _context.Signals
                .Where(s => !s.IsExecuted &&
                    (s.ExpiresAt == null || s.ExpiresAt > DateTime.UtcNow))
                .OrderByDescending(s => s.Confidence)
                .ToListAsync();
        }

        public async Task<List<Signal>> GetSignalHistoryAsync(int count = 50)
        {
            return await _context.Signals
                .OrderByDescending(s => s.GeneratedAt)
                .Take(count)
                .ToListAsync();
        }

        public async Task<Signal?> GetLatestSignalAsync(string symbol)
        {
            return await _context.Signals
                .Where(s => s.Symbol == symbol)
                .OrderByDescending(s => s.GeneratedAt)
                .FirstOrDefaultAsync();
        }
    }
}