// ViewModels/DashboardViewModel.cs
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using TradingBot.Models;

namespace TradingBot.ViewModels
{
    public class DashboardViewModel
    {
        public decimal TotalEquity { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal UnrealizedPnL { get; set; }
        public decimal RealizedPnL { get; set; }
        public decimal TotalPnL { get; set; }
        public decimal ReturnPercent { get; set; }
        public int OpenPositions { get; set; }
        public double WinRate { get; set; }
        public int TotalTrades { get; set; }
        public int ActiveStrategies { get; set; }
        public List<Trade> RecentTrades { get; set; } = new();
        public List<Trade> OpenTrades { get; set; } = new();
        public List<Signal> ActiveSignals { get; set; } = new();
        public Dictionary<string, decimal> LivePrices { get; set; } = new();
        public List<NewsSentiment> News { get; set; } = new List<NewsSentiment>();
    }

    public class TradeViewModel
    {
        public string Symbol { get; set; } = "BTCUSDT";
        public TradeType Type { get; set; }
        [Range(0.00000001, 1_000_000,ErrorMessage = "Quantity must be between 0.00000001 and 1,000,000 asset units.")]
        public decimal Quantity { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        public string? Strategy { get; set; }
        public List<string> AvailableSymbols { get; set; } = new()
        {
            "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT",
            "ADAUSDT", "XRPUSDT", "DOTUSDT", "DOGEUSDT"
        };
        public decimal CurrentPrice { get; set; }
        public decimal AccountBalance { get; set; }
    }

    public class BacktestViewModel
    {
        public int StrategyId { get; set; }
        public DateTime StartDate { get; set; } =
            DateTime.UtcNow.AddDays(-90);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
        public decimal InitialCapital { get; set; } = 100000;
        public List<Strategy> Strategies { get; set; } = new();
        public BacktestResult? Result { get; set; }
        public List<BacktestResult> History { get; set; } = new();
    }
}