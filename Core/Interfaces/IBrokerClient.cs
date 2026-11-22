// Place this in: Trading_Bot_Website/Core/Interfaces/IBrokerClient.cs
// OR if you choose Option 2: Trading_Bot_Website/Services/IBrokerClient.cs

using Microsoft.Extensions.Logging;
using TradingBot.Data;
using TradingBot.Models;

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
        Task<List<Trade>> GetOpenExchangePositionsAsync();
        Task<string> PlaceOcoOrderAsync(
            string symbol, string side, decimal quantity,
            decimal stopPrice, decimal limitPrice);
        Task<(decimal StopPrice, decimal LimitPrice)> NormalizeOcoPricesAsync(
            string symbol, decimal stopPrice, decimal limitPrice);
        Task SyncOpenTradesAsync(AppDbContext db, ILogger logger);
        Task<bool> CancelOrderAsync(string orderId);
        Task<bool> CancelOcoAsync(string symbol, string listClientOrderId);
        
        // 4. Order Status
        Task<object> GetOrderStatusAsync(string orderId); // You can create an OrderResponse model for this
    }

    public sealed class OcoPlacementOutcomeUnknownException : InvalidOperationException
    {
        public OcoPlacementOutcomeUnknownException(
            string clientOrderId, string message, Exception? innerException = null)
            : base(message, innerException)
        {
            ClientOrderId = clientOrderId;
        }

        public string ClientOrderId { get; }
    }
}