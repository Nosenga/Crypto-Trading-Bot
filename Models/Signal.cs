// Models/Signal.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public enum SignalType
    {
        Buy,
        Sell,
        Hold,
        StrongBuy,
        StrongSell
    }

    public class Signal
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public SignalType Type { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? TargetPrice { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? StopLossPrice { get; set; }

        [Range(0, 100)]
        public double Confidence { get; set; }

        [StringLength(100)]
        public string Source { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Reason { get; set; }

        public bool IsExecuted { get; set; }
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
    }
}