
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public enum TradeType
    {
        Buy,
        Sell
    }

    public enum TradeStatus
    {
        Pending,
        Executed,
        Closed,
        Cancelled,
        StopLossHit,
        TakeProfitHit
    }

    public enum OrderType
    {
        Market,
        Limit,
        StopLoss,
        TakeProfit
    }

    public class Trade
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Required]
        public TradeType Type { get; set; }

        public OrderType OrderType { get; set; } = OrderType.Market;

        [Column(TypeName = "decimal(18,8)")]
        public decimal EntryPrice { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? ExitPrice { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Quantity { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? StopLoss { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? TakeProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ProfitLoss { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Fees { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TotalValue { get; set; }

        public TradeStatus Status { get; set; } = TradeStatus.Pending;

        [StringLength(100)]
        public string? Strategy { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExecutedAt { get; set; }
        public DateTime? ClosedAt { get; set; }

        [StringLength(50)]
        public string? SignalSource { get; set; }

        public double Confidence { get; set; }
    }
}