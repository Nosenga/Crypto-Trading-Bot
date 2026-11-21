using TradingBot.Models;

namespace TradingBot.Core.Interfaces
{
    public interface IPriceFeed
    {
        Task<decimal> GetCurrentPriceAsync(string symbol);
        Task<Dictionary<string, decimal>> GetTickersAsync(IEnumerable<string> symbols);
        Task<List<MarketData>> GetHistoricalCandlesAsync(string symbol, DateTime from, DateTime to, string timeframe = "1h");
        Task<Dictionary<string, (decimal StepSize, decimal MinNotional)>> GetExchangeFiltersAsync(IEnumerable<string> symbols);
    }
}