namespace TradingBot.Services
{
    public interface INewsService
    {
        // Returns average sentiment from the last 2 hours (-1.0 to +1.0)
        Task<double> GetCurrentMarketSentimentAsync(); 
    }
}