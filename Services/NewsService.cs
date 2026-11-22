using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class NewsService : INewsService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NewsService> _logger;
        private readonly HttpClient _httpClient;

        // Bullish and Bearish keyword dictionaries
        private static readonly string[] BullishWords = { "surge", "rally", "bull", "approve", "adopt", "gain", "record", "soar", "etf", "inflow" };
        private static readonly string[] BearishWords = { "crash", "ban", "hack", "sec", "sue", "drop", "bear", "dump", "outflow", "scam", "lawsuit" };

        public NewsService(AppDbContext context, ILogger<NewsService> logger, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
        }

        public async Task<double> GetCurrentMarketSentimentAsync()
        {
            // Fetch news from the last 2 hours
            var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
            var recentNews = await _context.NewsSentiments
                .Where(n => n.PublishedAt >= twoHoursAgo)
                .ToListAsync();

            if (!recentNews.Any()) return 0.0; // Neutral if no news

            return recentNews.Average(n => n.Score);
        }

        public async Task FetchAndScoreNewsAsync()
        {
            try
            {
                // Using CoinTelegraph RSS feed (No API key required)
                var xml = await _httpClient.GetStringAsync("https://cointelegraph.com/rss");
                var doc = XDocument.Parse(xml);
                var items = doc.Descendants("item").Take(10); // Get last 10 articles

                foreach (var item in items)
                {
                    var title = item.Element("title")?.Value;
                    var pubDateStr = item.Element("pubDate")?.Value;

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(pubDateStr)) continue;

                    if (!DateTime.TryParse(pubDateStr, out var pubDate)) pubDate = DateTime.UtcNow;

                    // Check if we already have this article
                    if (await _context.NewsSentiments.AnyAsync(n => n.Headline == title)) continue;

                    // Calculate Sentiment Score
                    double score = 0.0;
                    var lowerTitle = title.ToLower();
                    
                    foreach (var word in BullishWords)
                        if (lowerTitle.Contains(word)) score += 0.2;

                    foreach (var word in BearishWords)
                        if (lowerTitle.Contains(word)) score -= 0.2;

                    // Clamp between -1.0 and 1.0
                    score = Math.Max(-1.0, Math.Min(1.0, score));

                    _context.NewsSentiments.Add(new NewsSentiment
                    {
                        Headline = title,
                        Source = "CoinTelegraph",
                        PublishedAt = pubDate.ToUniversalTime(),
                        Score = score
                    });
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch or score news from CoinTelegraph");
            }
        }
    }
}