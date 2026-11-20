// Controllers/StrategyController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Controllers
{
    [Authorize]
    public class StrategyController : Controller
    {
        private readonly AppDbContext _context;

        public StrategyController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var strategies = await _context.Strategies.ToListAsync();
            return View(strategies);
        }

        public IActionResult Create() => View();

        [HttpPost]
        public async Task<IActionResult> Create(Strategy strategy)
        {
            if (!ModelState.IsValid) return View(strategy);

            strategy.CreatedAt = DateTime.UtcNow;
            _context.Strategies.Add(strategy);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Strategy '{strategy.Name}' created!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var strategy = await _context.Strategies.FindAsync(id);
            if (strategy == null) return NotFound();

            strategy.Status = strategy.Status == StrategyStatus.Active
                ? StrategyStatus.Paused
                : StrategyStatus.Active;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var strategy = await _context.Strategies.FindAsync(id);
            if (strategy == null) return NotFound();

            _context.Strategies.Remove(strategy);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Strategy '{strategy.Name}' deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var strategy = await _context.Strategies.FindAsync(id);
            if (strategy == null) return NotFound();

            var trades = await _context.Trades
                .Where(t => t.Strategy == strategy.Name)
                .OrderByDescending(t => t.CreatedAt)
                .Take(50)
                .ToListAsync();

            ViewBag.Trades = trades;
            return View(strategy);
        }
    }
}