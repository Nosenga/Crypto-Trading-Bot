// Services/ITradingBotService.cs
using TradingBot.Models;

namespace TradingBot.Services
{
    public interface ITradingBotService
    {
        Task<Trade> ExecuteTradeAsync(
            string symbol, TradeType type, decimal quantity,
            string? strategy = null, decimal? stopLoss = null,
            decimal? takeProfit = null, double confidence = 80,
            string? signalSource = null);

        Task<Trade?> CloseTradeAsync(int tradeId);
        Task<List<Trade>> GetOpenTradesAsync();
        Task<List<Trade>> GetTradeHistoryAsync(int count = 100);
        Task<Portfolio> GetPortfolioAsync();
        Task<Portfolio> UpdatePortfolioAsync();
        Task<bool> CheckStopLossAndTakeProfitAsync();
        Task<Dictionary<string, object>> GetDashboardStatsAsync();

        /// <summary>
        /// Scans pending signals against Active strategies and automatically
        /// executes or closes trades on their behalf. Returns the number of
        /// actions taken.
        /// </summary>
        Task<int> RunAutoTradingCycleAsync();
    }
}