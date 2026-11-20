// Models/Portfolio.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public class Portfolio
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialBalance { get; set; } = 100000m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentBalance { get; set; } = 100000m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalEquity { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnrealizedPnL { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RealizedPnL { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPnL => RealizedPnL + UnrealizedPnL;

        public decimal ReturnPercent => InitialBalance > 0
            ? (TotalPnL / InitialBalance) * 100 : 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MaxDrawdown { get; set; }

        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }

        public double WinRate => TotalTrades > 0
            ? (double)WinningTrades / TotalTrades * 100 : 0;

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    public class PortfolioHolding
    {
        [Key]
        public int Id { get; set; }

        [StringLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,8)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal AverageEntryPrice { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal CurrentPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal MarketValue => Quantity * CurrentPrice;

        [Column(TypeName = "decimal(18,2)")]
        public decimal UnrealizedPnL => (CurrentPrice - AverageEntryPrice) * Quantity;

        public decimal UnrealizedPnLPercent => AverageEntryPrice > 0
            ? ((CurrentPrice - AverageEntryPrice) / AverageEntryPrice) * 100 : 0;
    }
}