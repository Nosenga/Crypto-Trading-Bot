using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TradingBot.ViewModels;  // ADD THIS
using TradingBot.Services;

namespace TradingBot.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ISafetyService _safetyService;
        private readonly ITradingBotService _tradingBotService;
        private readonly IConfiguration _configuration;

        public AdminController(
            ISafetyService safetyService,
            ITradingBotService tradingBotService,
            IConfiguration configuration)
        {
            _safetyService = safetyService;
            _tradingBotService = tradingBotService;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var portfolio = await _tradingBotService.GetPortfolioAsync();
            var dailyLossLimitPercent = _configuration.GetValue<decimal>(
                "TradingBot:RiskManagement:DailyLossLimitPercent", 3m);
            var dailyLossPercent = portfolio.StartOfDayBalance > 0
                ? (portfolio.StartOfDayBalance - portfolio.CurrentBalance) /
                  portfolio.StartOfDayBalance * 100
                : 0;

            var model = new AdminViewModel
            {
                IsArmed = _safetyService.IsArmed,
                IsKillSwitchActive = _safetyService.IsKillSwitchActive,
                KillSwitchAt = _safetyService.KillSwitchAt,
                KillSwitchReason = _safetyService.KillSwitchReason,
                HighWaterMark = portfolio.HighWaterMark,
                CurrentBalance = portfolio.CurrentBalance,
                StartOfDayBalance = portfolio.StartOfDayBalance,
                DailyLossLimitPercent = dailyLossLimitPercent,
                DailyLossPercent = dailyLossPercent,
                IsDailyLossLimitBreached = dailyLossPercent > dailyLossLimitPercent
            };
            return View(model);
        }

        [HttpPost]
        public IActionResult Arm()
        {
            _safetyService.ArmTrading();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Disarm()
        {
            _safetyService.DisarmTrading();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Activate(string reason)
        {
            _safetyService.ActivateKillSwitch(reason);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public IActionResult Deactivate()
        {
            _safetyService.DeactivateKillSwitch();
            return RedirectToAction(nameof(Index));
        }
    }
}