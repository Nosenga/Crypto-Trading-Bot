// Services/ISignalService.cs
using TradingBot.Models;

namespace TradingBot.Services
{
    public interface ISignalService
    {
        Task<List<Signal>> GenerateSignalsAsync(string symbol);
        Task<List<Signal>> GetActiveSignalsAsync();
        Task<List<Signal>> GetSignalHistoryAsync(int count = 50);
        Task<Signal?> GetLatestSignalAsync(string symbol);
    }
}