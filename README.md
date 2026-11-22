# TradingBot Pro - Automated Crypto Trading Platform

A fully-featured, production-ready algorithmic trading bot built with ASP.NET Core MVC, Entity Framework Core, and SignalR. It supports automated strategy execution, backtesting, live price streaming, and **direct Binance integration** for live market trading.

> 🚀 **Current Status:** Ready for Live Trading. The bot is fully compiled and authenticated with Binance. It is currently in a 24-hour security cooldown (standard for new Binance API keys) before executing its first real trade.

## ✨ Features

- **Live Binance Integration:** Fetches real-time prices directly from Binance via a secure `IBrokerClient` abstraction.
- **Automated Strategy Engine:** Built-in support for Moving Average Crossover, RSI Overbought/Oversold, MACD, and Bollinger Bands.
- **Obsidian UI:** Dark, minimalist, data-dense interface replacing Bootstrap — custom-styled panels (.p), grids (.grid), KPI cards (.kpis), and toast notifications.
- **Real-Time Dashboard:** Interactive UI with Chart.js, showing live equity curves, open positions, and trade history.
- **Multi-Mode Support:**
  - **Paper Mode:** Trade with fake money using live Binance data (perfect for testing).
  - **Live Mode:** Execute real market orders on Binance with built-in safety guards.
- **Risk Management:** Fixed fractional position sizing (`RiskPerTradePercent`), configurable position caps (`MaxPositionSize` and `MaxOpenPositions`), and configurable Stop-Loss/Take-Profit limits.
- **Daily Loss Limit:** Auto-disables trading when daily loss threshold is hit, with console warning "Daily Loss Limit Hit".
- **Kill Switch:** Emergency halt for all trading activity, activatable/deactivatable by admin.
- **Shadow Mode:** All trades executed in "paper" mode only — console logs `[Shadow Mode] Would have bought {Quantity} {Symbol}`, no orders sent to Binance.
- **Backtesting Engine:** Simulate historical performance and calculate Sharpe Ratio, Max Drawdown, and Win Rate.
- **SignalR Real-Time Updates:** WebSocket-style live price ticker updates directly on the dashboard.

## 🧰 Tech Stack

- **Framework:** .NET 8 (ASP.NET Core MVC)
- **Database:** SQLite (Entity Framework Core)
- **Real-Time Communication:** SignalR
- **Exchange API:** Binance.Net (v8.6.0)
- **Architecture:** Repository Pattern, Adapter Pattern (`IBrokerClient`), BackgroundService Worker
- **Frontend:** Obsidian UI design system (replacing Bootstrap 5), Chart.js, FontAwesome

## 📂 Project Structure

- `Controllers/` - MVC Controllers for Dashboard, Trading, Strategies, and Backtesting.
- `Services/` - Core business logic (MarketData, Signal, TradingBot, Backtest, Broker services).
- `Core/Interfaces/` - Abstractions including `IBrokerClient` for exchange-agnostic trading.
- `Models/` - Database entities (Trade, Strategy, Portfolio, Signal, MarketData).
- `Hubs/` - SignalR Hub (`TradingHub`) for pushing live prices to the UI.

## 🛠️ How to Run (Development)

1. Clone the repository.
2. Open the `.csproj` in VS Code or Visual Studio 2022+.
3. Restore NuGet packages: `dotnet restore`
4. **Run the application:** `dotnet run`
5. Navigate to `https://localhost:5021`.

> ⚠️ **Note:** The `tradingbot.db` file is gitignored. The app will automatically create the database and seed the initial `$1,000,000` portfolio and default strategies on the first run.

## 🔐 Admin Password

Generate a BCrypt hash locally with `TradingBot.Tools.PasswordHasher.Hash`; the application does not ship a password-hashing command. Store the hash in user-secrets:

```text
dotnet user-secrets init
dotnet user-secrets set "Admin:PasswordHash" "<the hash>"
```

Initialize user-secrets only once per project. The password hash is kept out of `appsettings.json`.
The application refuses to start if `Admin:PasswordHash` is empty. Generate the
hash by calling `TradingBot.Tools.PasswordHasher.Hash(plaintext)` from a local
C# runner, then store the result with the command above.

## 🔑 Configuration (appsettings.json)

```json
"TradingBot": {
  "Mode": "Paper", // Change to "Live" to use real Binance data
  "MaxOpenPositions": 10,
  "MaxPositionSize": 1000, // Hard cap on position value per trade
  "RiskPerTradePercent": 1.0,
  "RiskManagement": {
    "DailyLossLimitPercent": 3.0,
    "MaxDrawdownLimitPercent": 10.0
  },
  "DefaultStopLossPercent": 2.0,
  "DefaultTakeProfitPercent": 5.0,
  "Binance": {
    "ApiKey": "YOUR_REAL_KEY_HERE",
    "ApiSecret": "YOUR_REAL_SECRET_HERE",
    "EnableLiveOrders": false, // Set to true ONLY after explicit safety review
    "UseTestnet": true // Defaults to Testnet; set false only for approved live trading
  }
}