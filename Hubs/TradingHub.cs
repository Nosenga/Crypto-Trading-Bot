// Hubs/TradingHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TradingBot.Services;

namespace TradingBot.Hubs
{
    [Authorize]
    public class TradingHub : Hub
    {
        private readonly IMarketDataService _marketDataService;
        private readonly ITradingBotService _tradingService;

        public TradingHub(
            IMarketDataService marketDataService,
            ITradingBotService tradingService)
        {
            _marketDataService = marketDataService;
            _tradingService = tradingService;
        }

        public async Task SubscribeSymbol(string symbol)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId, $"symbol_{symbol}");
        }

        public async Task UnsubscribeSymbol(string symbol)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId, $"symbol_{symbol}");
        }

        public async Task GetLatestPrice(string symbol)
        {
            var prices = await _marketDataService
                .GetPriceUpdatesAsync(new List<string> { symbol });
            await Clients.Caller.SendAsync(
                "PriceUpdate", symbol, prices.GetValueOrDefault(symbol));
        }

        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Connected",
                new { ConnectionId = Context.ConnectionId });
            await base.OnConnectedAsync();
        }
    }
}