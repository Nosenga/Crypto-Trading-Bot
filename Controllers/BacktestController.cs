// Controllers/BacktestController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Models;
using TradingBot.Services;
using TradingBot.ViewModels;

namespace TradingBot.Controllers
{
    [Authorize]
    public class BacktestController : Controller
    {
        private readonly IBacktestService _backtestService;
        private readonly AppDbContext _context;

        public BacktestController(
            IBacktestService backtestService, AppDbContext context)
        {
            _backtestService = backtestService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new BacktestViewModel
            {
                Strategies = await _context.Strategies.ToListAsync(),
                History = await _backtestService.GetBacktestHistoryAsync()
            };
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> Run(BacktestViewModel model)
        {
            try
            {
                var result = await _backtestService.RunBacktestAsync(
                    model.StrategyId, model.StartDate, model.EndDate,
                    model.InitialCapital);

                TempData["Success"] =
                    $"Backtest complete! Return: {result.TotalReturnPercent}%";

                return RedirectToAction(nameof(Results), new { id = result.Id });
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> Results(int id)
        {
            var result = await _backtestService.GetBacktestResultAsync(id);
            if (result == null) return NotFound();

            return View(result);
        }
    }
}