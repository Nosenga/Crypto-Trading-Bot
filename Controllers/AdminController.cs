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

        public AdminController(ISafetyService safetyService)
        {
            _safetyService = safetyService;
        }

        public IActionResult Index()
        {
            var model = new AdminViewModel
            {
                IsKillSwitchActive = _safetyService.IsKillSwitchActive,
                KillSwitchAt = _safetyService.KillSwitchAt,
                KillSwitchReason = _safetyService.KillSwitchReason
            };
            return View(model);
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