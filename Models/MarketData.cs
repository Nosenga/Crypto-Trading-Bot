// Models/MarketData.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingBot.Models
{
    public class MarketData
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Symbol { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,8)")]
        public decimal Open { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal High { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Low { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Close { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal Volume { get; set; }

        public DateTime Timestamp { get; set; }

        // Technical indicators
        [Column(TypeName = "decimal(18,8)")]
        public decimal? SMA20 { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? SMA50 { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? EMA12 { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? EMA26 { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? RSI { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? MACDLine { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? MACDSignal { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? BollingerUpper { get; set; }

        [Column(TypeName = "decimal(18,8)")]
        public decimal? BollingerLower { get; set; }
    }
}