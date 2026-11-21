// Place this in: Trading_Bot_Website/Core/Interfaces/IBrokerClient.cs
// OR if you choose Option 2: Trading_Bot_Website/Services/IBrokerClient.cs

using TradingBot.Models; // So you can use your existing MarketData model

namespace TradingBot.Core.Interfaces
{
    public interface IBrokerClient
    {
        Task InitializeAsync(IEnumerable<string> symbols);
        Task<decimal> NormalizeQuantityAsync(string symbol, decimal quantity);

        // 1. Live Data
        Task<decimal> GetCurrentPriceAsync(string symbol);
        Task<List<MarketData>> GetHistoricalCandlesAsync(string symbol, DateTime from, DateTime to, string timeframe = "1h");

        // 2. Account Info
        Task<decimal> GetAvailableBalanceAsync(string quoteCurrency); // e.g. "USDT" or "ZAR"

        // 3. Order Execution
        Task<string> PlaceMarketOrderAsync(string symbol, string side, decimal quantity);
        Task<string> PlaceClosingMarketOrderAsync(string symbol, string side, decimal quantity);
        Task<string> PlaceLimitOrderAsync(string symbol, string side, decimal quantity, decimal price);
        Task<bool> CancelOrderAsync(string orderId);
        
        // 4. Order Status
        Task<object> GetOrderStatusAsync(string orderId); // You can create an OrderResponse model for this
    }
}