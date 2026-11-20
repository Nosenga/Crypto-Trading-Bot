// Services/IMarketDataService.cs
using TradingBot.Models;

namespace TradingBot.Services
{
    public interface IMarketDataService
    {
        Task<List<MarketData>> GetHistoricalDataAsync(string symbol, DateTime from, DateTime to);
        Task<MarketData?> GetLatestPriceAsync(string symbol);
        Task<List<MarketData>> GenerateSimulatedDataAsync(string symbol, int days = 30);
        Task<Dictionary<string, decimal>> GetPriceUpdatesAsync(List<string> symbols);
        Task<List<MarketData>> CalculateIndicatorsAsync(List<MarketData> data);
        Task AppendLiveCandleAsync(string symbol, decimal price);
    }
}