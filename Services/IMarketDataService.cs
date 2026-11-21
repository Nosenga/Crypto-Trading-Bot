using TradingBot.Models;

namespace TradingBot.Services
{
    public interface IMarketDataService
    {
        Task<List<MarketData>> GetHistoricalDataAsync(string symbol, DateTime from, DateTime to);
        Task<MarketData?> GetLatestPriceAsync(string symbol);
        Task<Dictionary<string, decimal>> GetPriceUpdatesAsync(List<string> symbols);
        Task<List<MarketData>> CalculateIndicatorsAsync(List<MarketData> data);
        
        // Replaces GenerateSimulatedDataAsync and AppendLiveCandleAsync
        Task RefreshRecentCandlesAsync(string symbol, string timeframe = "1m", int limit = 100);
    }
}