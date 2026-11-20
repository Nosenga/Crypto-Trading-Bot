// Controllers/DashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingBot.Services;
using TradingBot.ViewModels;

namespace TradingBot.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ITradingBotService _tradingService;
        private readonly ISignalService _signalService;
        private readonly IMarketDataService _marketService;

        public DashboardController(
            ITradingBotService tradingService,
            ISignalService signalService,
            IMarketDataService marketService)
        {
            _tradingService = tradingService;
            _signalService = signalService;
            _marketService = marketService;
        }

        public async Task<IActionResult> Index()
        {
            var stats = await _tradingService.GetDashboardStatsAsync();
            var signals = await _signalService.GetActiveSignalsAsync();

            var symbols = new List<string>
            {
                "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT"
            };
            var prices = await _marketService.GetPriceUpdatesAsync(symbols);

            var viewModel = new DashboardViewModel
            {
                TotalEquity = (decimal)stats["TotalEquity"],
                CurrentBalance = (decimal)stats["CurrentBalance"],
                UnrealizedPnL = (decimal)stats["UnrealizedPnL"],
                RealizedPnL = (decimal)stats["RealizedPnL"],
                TotalPnL = (decimal)stats["TotalPnL"],
                ReturnPercent = (decimal)stats["ReturnPercent"],
                OpenPositions = (int)stats["OpenPositions"],
                WinRate = (double)stats["WinRate"],
                TotalTrades = (int)stats["TotalTrades"],
                ActiveStrategies = (int)stats["ActiveStrategies"],
                RecentTrades = (List<Models.Trade>)stats["RecentTrades"],
                OpenTrades = (List<Models.Trade>)stats["OpenTradeList"],
                ActiveSignals = signals,
                LivePrices = prices
            };

            return View(viewModel);
        }
    }
}