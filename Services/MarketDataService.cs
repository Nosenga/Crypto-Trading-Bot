// Services/MarketDataService.cs
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
        private readonly Random _random = new();
        private readonly IBrokerClient _brokerClient;

        // Simulated real-time prices
        private static readonly Dictionary<string, decimal> _livePrices = new()
        {
            ["BTCUSDT"] = 43500m,
            ["ETHUSDT"] = 2650m,
            ["BNBUSDT"] = 310m,
            ["ADAUSDT"] = 0.45m,
            ["SOLUSDT"] = 98m,
            ["XRPUSDT"] = 0.62m,
            ["DOTUSDT"] = 7.50m,
            ["DOGEUSDT"] = 0.085m
        };

        public MarketDataService(AppDbContext context, ILogger<MarketDataService> logger, IBrokerClient brokerClient)
        {
            _brokerClient = brokerClient;
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

        public async Task<List<MarketData>> GenerateSimulatedDataAsync(
            string symbol, int days = 30)
        {
            var data = new List<MarketData>();
            var basePrice = _livePrices.GetValueOrDefault(symbol, 100m);
            var startDate = DateTime.UtcNow.AddDays(-days);
            var currentPrice = basePrice * (1 - (decimal)(_random.NextDouble() * 0.2));

            for (int i = 0; i < days * 24; i++) // hourly data
            {
                var timestamp = startDate.AddHours(i);

                // Simulate realistic price movement (random walk with trend)
                var changePercent = (decimal)(_random.NextDouble() * 0.04 - 0.018);
                var newPrice = currentPrice * (1 + changePercent);

                var high = newPrice * (1 + (decimal)(_random.NextDouble() * 0.01));
                var low = newPrice * (1 - (decimal)(_random.NextDouble() * 0.01));
                var open = currentPrice;
                var close = newPrice;
                var volume = (decimal)(_random.NextDouble() * 1000 + 100);

                data.Add(new MarketData
                {
                    Symbol = symbol,
                    Open = Math.Round(open, 8),
                    High = Math.Round(high, 8),
                    Low = Math.Round(low, 8),
                    Close = Math.Round(close, 8),
                    Volume = Math.Round(volume, 8),
                    Timestamp = timestamp
                });

                currentPrice = newPrice;
            }

            // Calculate technical indicators
            data = await CalculateIndicatorsAsync(data);

            // Save to database
            _context.MarketData.AddRange(data);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Generated {Count} data points for {Symbol}", data.Count, symbol);

            return data;
        }

        public async Task<Dictionary<string, decimal>> GetPriceUpdatesAsync(
            List<string> symbols)
        {
            var prices = new Dictionary<string, decimal>();

            foreach (var symbol in symbols)
            {
               var price = await _brokerClient.GetCurrentPriceAsync(symbol);
               prices[symbol] = price;
            }

            return prices;
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
                if (data[i].EMA12.HasValue && data[i].EMA26.HasValue)
                {
                    data[i].MACDLine = Math.Round(
                        data[i].EMA12.Value - data[i].EMA26.Value, 8);
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

        public async Task AppendLiveCandleAsync(string symbol, decimal price)
        {
            var recent = await _context.MarketData
                .Where(m => m.Symbol == symbol)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // Not enough history yet for indicators to mean anything — backfill once.
            if (recent.Count < 60)
            {
                await GenerateSimulatedDataAsync(symbol, days: 5);
                recent = await _context.MarketData
                    .Where(m => m.Symbol == symbol)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();
            }

            var last = recent.LastOrDefault();
            var open = last?.Close ?? price;
            // Logical increment (not wall-clock) so timestamps never collide
            // with the append cadence and stay ordered relative to backfilled data.
            var timestamp = (last?.Timestamp ?? DateTime.UtcNow).AddMinutes(1);

            var candle = new MarketData
            {
                Symbol = symbol,
                Open = open,
                Close = price,
                High = Math.Round(Math.Max(open, price) * 1.0005m, 8),
                Low = Math.Round(Math.Min(open, price) * 0.9995m, 8),
                Volume = Math.Round((decimal)(_random.NextDouble() * 1000 + 100), 8),
                Timestamp = timestamp
            };

            _context.MarketData.Add(candle);
            recent.Add(candle);

            // Recompute indicators over the trailing window and persist.
            var window = recent.Skip(Math.Max(0, recent.Count - 120)).ToList();
            await CalculateIndicatorsAsync(window);

            await _context.SaveChangesAsync();
        }

        private List<decimal> CalculateEMA(List<decimal> prices, int period)
        {
            var ema = new List<decimal>();
            var multiplier = 2m / (period + 1);

            // First EMA = SMA
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