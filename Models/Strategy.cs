// Models/Strategy.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public enum StrategyType
    {
        MovingAverageCross,
        RSI,
        MACD,
        BollingerBands,
        MomentumBreakout,
        MeanReversion,
        GridTrading,
        Custom
    }

    public enum StrategyStatus
    {
        Active,
        Paused,
        Stopped
    }

    public class Strategy
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public StrategyType Type { get; set; }
        public StrategyStatus Status { get; set; } = StrategyStatus.Active;

        [StringLength(20)]
        public string Symbol { get; set; } = "BTCUSDT";

        // Risk parameters
        [Column(TypeName = "decimal(18,2)")]
        public decimal StopLossPercent { get; set; } = 2.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TakeProfitPercent { get; set; } = 5.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RiskPerTradePercent { get; set; } = 1.0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxPositionSize { get; set; } = 10000m;

        // Strategy-specific parameters (JSON)
        [StringLength(2000)]
        public string Parameters { get; set; } = "{}";

        // Performance tracking
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalProfitLoss { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDrawdown { get; set; }

        public double WinRate => TotalTrades > 0
            ? (double)WinningTrades / TotalTrades * 100
            : 0;

        public double ProfitFactor => LosingTrades > 0
            ? Math.Abs(WinningTrades / (double)LosingTrades)
            : 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastExecutedAt { get; set; }
    }
}