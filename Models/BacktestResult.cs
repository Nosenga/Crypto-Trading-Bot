// Models/BacktestResult.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public class BacktestResult
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string StrategyName { get; set; } = string.Empty;

        [StringLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialCapital { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalCapital { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalReturn { get; set; }

        public double TotalReturnPercent { get; set; }
        public double AnnualizedReturn { get; set; }
        public double SharpeRatio { get; set; }
        public double MaxDrawdownPercent { get; set; }

        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LargestWin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal LargestLoss { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageWin { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AverageLoss { get; set; }

        [StringLength(2000)]
        public string? EquityCurveJson { get; set; }

        [StringLength(5000)]
        public string? TradeLogJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}