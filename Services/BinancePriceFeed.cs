using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot;
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
                    Timestamp = k.OpenTime,

                    //Phase 2c: Populate the new fields
                    CloseTime = k.CloseTime,
                    IsClosed = k.CloseTime <= DateTime.UtcNow,
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve klines for {Symbol}", symbol);
                return new List<MarketData>();
            }
        }

        public async Task<Dictionary<string, (decimal StepSize, decimal MinNotional)>> GetExchangeFiltersAsync(IEnumerable<string> symbols)
        {
            var filters = new Dictionary<string, (decimal, decimal)>(StringComparer.OrdinalIgnoreCase);
            var symbolList = symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var client = new BinanceClient();
            var result = await client.SpotApi.ExchangeData.GetExchangeInfoAsync();

            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"Failed to fetch Binance exchange filters: {result.Error?.Message}");
            }

            foreach (var symbolInfo in result.Data.Symbols.Where(s => symbolList.Contains(s.Name)))
            {
                var lotSizeFilter = symbolInfo.Filters
                    .OfType<BinanceSymbolLotSizeFilter>()
                    .FirstOrDefault();
                var notionalFilter = symbolInfo.Filters
                    .OfType<BinanceSymbolNotionalFilter>()
                    .FirstOrDefault();
                var minNotionalFilter = symbolInfo.Filters
                    .OfType<BinanceSymbolMinNotionalFilter>()
                    .FirstOrDefault();
                var minNotional = notionalFilter?.MinNotional
                    ?? minNotionalFilter?.MinNotional;

                if (lotSizeFilter == null ||
                    lotSizeFilter.StepSize <= 0 ||
                    minNotional == null ||
                    minNotional <= 0)
                {
                    throw new InvalidOperationException(
                        $"Binance exchange filters are incomplete for {symbolInfo.Name}.");
                }

                filters[symbolInfo.Name] = (lotSizeFilter.StepSize, minNotional.Value);
            }

            var missingSymbols = symbolList
                .Where(symbol => !filters.ContainsKey(symbol))
                .ToArray();
            if (missingSymbols.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Binance exchange filters were not returned for: {string.Join(", ", missingSymbols)}.");
            }

            return filters;
        }
    }
}