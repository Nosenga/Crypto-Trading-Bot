// Services/IBacktestService.cs
using TradingBot.Models;

namespace TradingBot.Services
{
    public interface IBacktestService
    {
        Task<BacktestResult> RunBacktestAsync(
            int strategyId, DateTime startDate, DateTime endDate,
            decimal initialCapital = 100000);

        Task<List<BacktestResult>> GetBacktestHistoryAsync();
        Task<BacktestResult?> GetBacktestResultAsync(int id);
    }
}