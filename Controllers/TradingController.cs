// Controllers/TradingController.cs
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
    public class TradingController : Controller
    {
        private readonly ITradingBotService _tradingService;
        private readonly IMarketDataService _marketService;
        private readonly AppDbContext _context;

        public TradingController(
            ITradingBotService tradingService,
            IMarketDataService marketService,
            AppDbContext context)
        {
            _tradingService = tradingService;
            _marketService = marketService;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var portfolio = await _tradingService.GetPortfolioAsync();
            var openTrades = await _tradingService.GetOpenTradesAsync();
            var tradeHistory = await _tradingService.GetTradeHistoryAsync(50);

            ViewBag.Portfolio = portfolio;
            ViewBag.OpenTrades = openTrades;
            ViewBag.TradeHistory = tradeHistory;

            return View(new TradeViewModel
            {
                AccountBalance = portfolio.CurrentBalance
            });
        }

        [HttpPost]
        public async Task<IActionResult> Execute(TradeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var trade = await _tradingService.ExecuteTradeAsync(
                    model.Symbol, model.Type, model.Quantity,
                    model.Strategy, model.StopLoss, model.TakeProfit);

                TempData["Success"] =
                    $"{model.Type} order executed for {model.Quantity} " +
                    $"{model.Symbol} at ${trade.EntryPrice:F2}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CloseTrade(int id)
        {
            try
            {
                var trade = await _tradingService.CloseTradeAsync(id);
                if (trade != null)
                {
                    TempData["Success"] =
                        $"Trade #{id} closed. P&L: ${trade.ProfitLoss:F2}";
                }
                else
                {
                    TempData["Error"] = "Trade not found or already closed.";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetPrice(string symbol)
        {
            var prices = await _marketService
                .GetPriceUpdatesAsync(new List<string> { symbol });
            return Json(prices.GetValueOrDefault(symbol, 0));
        }
    }
}