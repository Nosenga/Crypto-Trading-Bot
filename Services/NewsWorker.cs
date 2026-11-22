namespace TradingBot.Services
{
    public class NewsWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<NewsWorker> _logger;

        public NewsWorker(IServiceProvider serviceProvider, ILogger<NewsWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("NewsWorker started.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                using var scope = _serviceProvider.CreateScope();
                var newsService = scope.ServiceProvider.GetRequiredService<NewsService>();
                
                await newsService.FetchAndScoreNewsAsync();
                
                // Fetch every 15 minutes
                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
    }
}