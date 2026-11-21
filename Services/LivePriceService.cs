// Services/LivePriceService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;  // ← THIS is what brings in BackgroundService
using TradingBot.Core.Interfaces;
using TradingBot.Hubs;

namespace TradingBot.Services
{
    public class LivePriceService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHubContext<TradingHub> _hubContext;
        private readonly ILogger<LivePriceService> _logger;

        private readonly List<string> _symbols = new()
        {
            "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT",
            "ADAUSDT", "XRPUSDT", "DOTUSDT", "DOGEUSDT"
        };

        public LivePriceService(
            IServiceProvider serviceProvider,
            IHubContext<TradingHub> hubContext,
            ILogger<LivePriceService> logger)
        {
            _serviceProvider = serviceProvider;
            _hubContext = hubContext;
            _logger = logger;
        }

        // Candle/signal/auto-trade work runs every Nth tick (~15s at a 5s tick
        // rate) rather than every tick, so indicator history builds up as
        // meaningful candles instead of one row every 5 seconds.
        private const int SignalEngineEveryNTicks = 3;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LivePriceService started.");
            using (var initScope = _serviceProvider.CreateScope())
            {
                var broker = initScope.ServiceProvider
                    .GetRequiredService<IBrokerClient>();
                await broker.InitializeAsync(_symbols);
            }

            var tick = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var marketService = scope.ServiceProvider
                        .GetRequiredService<IMarketDataService>();
                    var tradingService = scope.ServiceProvider
                        .GetRequiredService<ITradingBotService>();

                    // Get latest prices and broadcast
                    var prices = await marketService
                        .GetPriceUpdatesAsync(_symbols);

                    await _hubContext.Clients.All
                        .SendAsync("MarketUpdate", prices, stoppingToken);

                    if (tick % SignalEngineEveryNTicks == 0)
                    {
                        await RunSignalEngineAsync(prices);
                    }

                    // Check stop-loss / take-profit
                    await tradingService.CheckStopLossAndTakeProfitAsync();

                    // Update portfolio
                    await tradingService.UpdatePortfolioAsync();
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown — don't log as error
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in LivePriceService loop");
                }

                tick++;
                await Task.Delay(5000, stoppingToken);
            }

            _logger.LogInformation("LivePriceService stopped.");
        }

        /// <summary>
        /// Appends a live candle + recalculates indicators + generates
        /// signals for every tracked symbol, then lets the trading service
        /// act on any fresh signals for Active strategies. Each symbol gets
        /// its own DbContext scope so one bad symbol can't corrupt the
        /// change-tracker for the rest of the cycle.
        /// </summary>
        private async Task RunSignalEngineAsync(Dictionary<string, decimal> prices)
        {
            foreach (var symbol in _symbols)
            {
                if (!prices.TryGetValue(symbol, out var price) || price <= 0)
                    continue;

                try
                {
                    using var symbolScope = _serviceProvider.CreateScope();
                    var marketService = symbolScope.ServiceProvider
                        .GetRequiredService<IMarketDataService>();
                    var signalService = symbolScope.ServiceProvider
                        .GetRequiredService<ISignalService>();

                    // Fetch real 1m candles from Binance and update DB
                    await marketService.RefreshRecentCandlesAsync(symbol, "1m", 100);
                    
                    // Generate signals based on the updated real candles
                    await signalService.GenerateSignalsAsync(symbol);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Signal engine skipped {Symbol} this cycle", symbol);
                }
            }

            try
            {
                using var tradeScope = _serviceProvider.CreateScope();
                var tradingService = tradeScope.ServiceProvider
                    .GetRequiredService<ITradingBotService>();

                var actions = await tradingService.RunAutoTradingCycleAsync();
                if (actions > 0)
                    _logger.LogInformation(
                        "Auto-trading cycle executed {Count} action(s)", actions);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Auto-trading cycle failed");
            }
        }
    }
}