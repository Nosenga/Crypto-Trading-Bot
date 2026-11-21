using Binance.Net.Clients;
using Binance.Net.Enums;
using TradingBot.Core.Interfaces;
using TradingBot.Models;

namespace TradingBot.Services
{
    public sealed class BinancePriceFeed : IPriceFeed
    {
        private readonly ILogger<BinancePriceFeed> _logger;

        public BinancePriceFeed(ILogger<BinancePriceFeed> logger)
        {
            _logger = logger;
        }

        public async Task<decimal> GetCurrentPriceAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol is required.", nameof(symbol));

            try
            {
                using var client = new BinanceClient();
                var result = await client.SpotApi.ExchangeData.GetPriceAsync(symbol);

                if (!result.Success || result.Data.Price <= 0)
                {
                    _logger.LogWarning(
                        "Binance price request for {Symbol} failed: {Error}",
                        symbol, result.Error?.Message ?? "invalid price");
                    return 0m;
                }

                return result.Data.Price;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve price for {Symbol}", symbol);
                return 0m;
            }
        }

        public async Task<Dictionary<string, decimal>> GetTickersAsync(IEnumerable<string> symbols)
        {
            var result = new Dictionary<string, decimal>();
            var symbolList = symbols?.ToList() ?? new List<string>();
            if (symbolList.Count == 0) return result;

            try
            {
                using var client = new BinanceClient();
                var tickers = await client.SpotApi.ExchangeData.GetTickersAsync(symbolList);

                if (!tickers.Success)
                {
                    _logger.LogWarning(
                        "Binance bulk ticker request failed: {Error}",
                        tickers.Error?.Message);
                    return result;
                }

                foreach (var t in tickers.Data)
                {
                    if (!string.IsNullOrEmpty(t.Symbol) && t.LastPrice > 0)
                        result[t.Symbol] = t.LastPrice;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve bulk tickers");
            }

            return result;
        }

        public async Task<List<MarketData>> GetHistoricalCandlesAsync(
            string symbol, DateTime from, DateTime to, string timeframe = "1h")
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
                    symbol, interval, startTime: from, endTime: to, limit: 1000);

                if (!result.Success)
                {
                    _logger.LogWarning(
                        "Binance klines for {Symbol} failed: {Error}",
                        symbol, result.Error?.Message);
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
                _logger.LogError(ex, "Failed to retrieve klines for {Symbol}", symbol);
                return new List<MarketData>();
            }
        }
    }
}