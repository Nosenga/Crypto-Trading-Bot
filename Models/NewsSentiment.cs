using System.ComponentModel.DataAnnotations;

namespace TradingBot.Models
{
    public class NewsSentiment
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Headline { get; set; } = string.Empty;
        
        public string Source { get; set; } = string.Empty;
        
        public DateTime PublishedAt { get; set; }
        
        // Score from -1.0 (Very Bearish) to +1.0 (Very Bullish)
        public double Score { get; set; } 
        
        public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    }
}