using Microsoft.EntityFrameworkCore;
using TradingBot.Core.Interfaces;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class MarketDataService : IMarketDataService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<MarketDataService> _logger;
        private readonly IPriceFeed _priceFeed;

        public MarketDataService(
            AppDbContext context,
            ILogger<MarketDataService> logger,
            IPriceFeed priceFeed)
        {
            _priceFeed = priceFeed;
            _context = context;
            _logger = logger;
        }

        public async Task<List<MarketData>> GetHistoricalDataAsync(
            string symbol, DateTime from, DateTime to)
        {
            return await _context.MarketData
                .Where(m => m.Symbol == symbol &&
                            m.Timestamp >= from &&
                            m.Timestamp <= to)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<MarketData?> GetLatestPriceAsync(string symbol)
        {
            return await _context.MarketData
                .Where(m => m.Symbol == symbol)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();
        }

        public async Task<Dictionary<string, decimal>> GetPriceUpdatesAsync(
            List<string> symbols)
        {
            return await _priceFeed.GetTickersAsync(symbols);
        }

        public async Task<List<MarketData>> CalculateIndicatorsAsync(
            List<MarketData> data)
        {
            if (data.Count < 50) return data;

            var closes = data.Select(d => d.Close).ToList();

            // SMA 20
            for (int i = 19; i < data.Count; i++)
                data[i].SMA20 = Math.Round(
                    closes.Skip(i - 19).Take(20).Average(), 8);

            // SMA 50
            for (int i = 49; i < data.Count; i++)
                data[i].SMA50 = Math.Round(
                    closes.Skip(i - 49).Take(50).Average(), 8);

            // EMA 12
            var ema12 = CalculateEMA(closes, 12);
            for (int i = 0; i < ema12.Count; i++)
                data[i + 11].EMA12 = Math.Round(ema12[i], 8);

            // EMA 26
            var ema26 = CalculateEMA(closes, 26);
            for (int i = 0; i < ema26.Count; i++)
                data[i + 25].EMA26 = Math.Round(ema26[i], 8);

            // RSI 14
            var rsi = CalculateRSI(closes, 14);
            for (int i = 0; i < rsi.Count; i++)
                data[i + 14].RSI = Math.Round(rsi[i], 2);

            // MACD
            for (int i = 25; i < data.Count; i++)
            {
                if (data[i].EMA12 is decimal ema12Value &&
                    data[i].EMA26 is decimal ema26Value)
                {
                    data[i].MACDLine = Math.Round(
                        ema12Value - ema26Value, 8);
                }
            }

            // Bollinger Bands (20-period, 2 std dev)
            for (int i = 19; i < data.Count; i++)
            {
                var window = closes.Skip(i - 19).Take(20).ToList();
                var sma = window.Average();
                var stdDev = (decimal)Math.Sqrt(
                    (double)window.Select(x =>
                        (double)(x - sma) * (double)(x - sma)).Average());
                data[i].BollingerUpper = Math.Round(sma + 2 * stdDev, 8);
                data[i].BollingerLower = Math.Round(sma - 2 * stdDev, 8);
            }

            return await Task.FromResult(data);
        }

        public async Task RefreshRecentCandlesAsync(
            string symbol, string timeframe = "1m", int limit = 100)
        {
            // 1. Calculate the time window based on timeframe and limit
            var minutesPerCandle = timeframe.ToLowerInvariant() switch
            {
                "1m" => 1,
                "5m" => 5,
                "15m" => 15,
                "1h" => 60,
                "4h" => 240,
                "1d" => 1440,
                _ => 1
            };

            var to = DateTime.UtcNow;
            var from = to.AddMinutes(-minutesPerCandle * limit);

            // 2. Fetch real klines from Binance via the PriceFeed
            var newCandles = await _priceFeed.GetHistoricalCandlesAsync(symbol, from, to, timeframe);

            if (!newCandles.Any())
            {
                _logger.LogWarning("No new candles fetched for {Symbol} ({Timeframe})", symbol, timeframe);
                return;
            }

            // 3. Fetch existing candles in this time window from DB
            var existing = await _context.MarketData
                .Where(m => m.Symbol == symbol && m.Timestamp >= from)
                .ToListAsync();

            // 4. Upsert into the database
            foreach (var candle in newCandles)
            {
                var match = existing.FirstOrDefault(e => e.Timestamp == candle.Timestamp);
                if (match != null)
                {
                    // Update existing candle (it might have been incomplete on the last fetch)
                    match.Open = candle.Open;
                    match.High = candle.High;
                    match.Low = candle.Low;
                    match.Close = candle.Close;
                    match.Volume = candle.Volume;

                    // Phase 2c: Update the closed status
                    match.CloseTime = candle.CloseTime;
                    match.IsClosed = candle.IsClosed;
                }
                else
                {
                    // Add new candle
                    _context.MarketData.Add(candle);
                }
            }

            await _context.SaveChangesAsync();

            // 5. Recalculate indicators over a larger window (e.g. 200 candles) to ensure accuracy
            var allRecent = await _context.MarketData
                .Where(m => m.Symbol == symbol)
                .OrderByDescending(m => m.Timestamp)
                .Take(200)
                .ToListAsync();

            allRecent.Reverse();
            await CalculateIndicatorsAsync(allRecent);
            await _context.SaveChangesAsync();
        }

        private List<decimal> CalculateEMA(List<decimal> prices, int period)
        {
            var ema = new List<decimal>();
            var multiplier = 2m / (period + 1);
            var firstSma = prices.Take(period).Average();
            ema.Add(firstSma);

            for (int i = period; i < prices.Count; i++)
            {
                var value = (prices[i] - ema.Last()) * multiplier + ema.Last();
                ema.Add(value);
            }

            return ema;
        }

        private List<decimal> CalculateRSI(List<decimal> prices, int period)
        {
            var rsi = new List<decimal>();
            var gains = new List<decimal>();
            var losses = new List<decimal>();

            for (int i = 1; i < prices.Count; i++)
            {
                var change = prices[i] - prices[i - 1];
                gains.Add(change > 0 ? change : 0);
                losses.Add(change < 0 ? Math.Abs(change) : 0);
            }

            for (int i = period - 1; i < gains.Count; i++)
            {
                var avgGain = gains.Skip(i - period + 1).Take(period).Average();
                var avgLoss = losses.Skip(i - period + 1).Take(period).Average();

                if (avgLoss == 0)
                    rsi.Add(100);
                else
                {
                    var rs = avgGain / avgLoss;
                    rsi.Add(Math.Round(100 - (100 / (1 + rs)), 2));
                }
            }

            return rsi;
        }
    }
}