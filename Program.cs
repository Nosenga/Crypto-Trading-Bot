using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TradingBot.Data;
using TradingBot.Hubs;
using TradingBot.Models;
using TradingBot.Services;
using TradingBot.Core.Interfaces;

var builder = WebApplication.CreateBuilder(args);

var adminPasswordHash = builder.Configuration["Admin:PasswordHash"];
if (string.IsNullOrWhiteSpace(adminPasswordHash))
{
    const string message =
        "Admin:PasswordHash is required. Generate a BCrypt hash with " +
        "TradingBot.Tools.PasswordHasher.Hash(plaintext), then set it with " +
        "`dotnet user-secrets set \"Admin:PasswordHash\" \"<hash>\"`.";
    Console.Error.WriteLine(message);
    throw new InvalidOperationException(message);
}

// --- 1. DATABASE ---
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 2. READ CONFIGURATION ---
var tradingMode = builder.Configuration["TradingBot:Mode"] ?? "Paper";
var enableLiveOrders = builder.Configuration.GetValue<bool>("TradingBot:Binance:EnableLiveOrders");
var liveAcknowledged = builder.Configuration.GetValue<bool>("TradingBot:Safety:LiveTradingAcknowledged");
var refuseIfNotAcknowledged = builder.Configuration.GetValue<bool>("TradingBot:Safety:RefuseLiveIfNotAcknowledged", true);

builder.Services.AddSingleton<IPriceFeed, BinancePriceFeed>();

// --- 2a. STARTUP SAFETY GUARD ---
// Refuse to start in Live mode with live orders enabled unless the operator
// has explicitly acknowledged the risk in config. This is the third gate:
// Mode=Live + EnableLiveOrders=true + LiveTradingAcknowledged=true.
if (tradingMode.Equals("Live", StringComparison.OrdinalIgnoreCase) && enableLiveOrders)
{
    if (refuseIfNotAcknowledged && !liveAcknowledged)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("==============================================================");
        Console.WriteLine(" REFUSING TO START");
        Console.WriteLine(" Mode=Live AND EnableLiveOrders=true, but");
        Console.WriteLine(" TradingBot:Safety:LiveTradingAcknowledged is not true.");
        Console.WriteLine("");
        Console.WriteLine(" This is a deliberate safety gate. Real money is at risk.");
        Console.WriteLine(" Set TradingBot:Safety:LiveTradingAcknowledged=true only");
        Console.WriteLine(" after you have read the security review and tested on");
        Console.WriteLine(" Binance Spot Testnet.");
        Console.WriteLine("==============================================================");
        Console.ResetColor();
        return; // exit before building the app
    }

    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("==============================================================");
    Console.WriteLine(" !! LIVE TRADING MODE - REAL MONEY AT RISK !!               ");
    Console.WriteLine(" Mode=Live, EnableLiveOrders=true, acknowledgement given.  ");
    Console.WriteLine(" Orders will be sent to Binance with REAL funds.          ");
    Console.WriteLine("==============================================================");
    Console.ResetColor();
}

// --- 3. REGISTER BROKER ---
if (tradingMode.Equals("Live", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IBrokerClient, BinanceBroker>();
    builder.Services.AddScoped<IMarketDataService, MarketDataService>();

    Console.WriteLine("=========================================");
    Console.WriteLine(" BOT STARTED IN LIVE DATA MODE");
    Console.WriteLine(" Real prices from Binance.");
    Console.WriteLine(enableLiveOrders
        ? " LIVE ORDERS ENABLED - real money at risk."
        : " LIVE ORDERS DISABLED - orders will be rejected by broker.");
    Console.WriteLine("=========================================");
}
else
{
    builder.Services.AddSingleton<IBrokerClient, PaperBroker>();
    builder.Services.AddScoped<IMarketDataService, MarketDataService>();

    Console.WriteLine("=========================================");
    Console.WriteLine(" BOT STARTED IN PAPER MODE");
    Console.WriteLine(" No real money at risk.");
    Console.WriteLine("=========================================");
}

// --- 4. OTHER SERVICES ---
builder.Services.AddScoped<ITradingBotService, TradingBotService>();
builder.Services.AddScoped<ISignalService, SignalService>();
builder.Services.AddScoped<IBacktestService, BacktestService>();

// --- Safety Service (in-memory kill switch) ---
builder.Services.AddSingleton<ISafetyService, SafetyService>();

// --- 5. BACKGROUND SERVICES ---
builder.Services.AddHostedService<LivePriceService>();

// --- 6. SIGNALR ---
builder.Services.AddSignalR();

// --- 7. MVC + ANTIFORGERY ---
builder.Services.AddControllersWithViews(options =>
{
    // Validate antiforgery on every POST by default.
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// --- 8. AUTHENTICATION (cookie, single admin user) ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- 9. DATABASE MIGRATION ---
// Phase 0: keep EnsureCreated for now (migrations come in Phase 2).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // apply migrations if any
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

    if (tradingMode.Equals("Live", StringComparison.OrdinalIgnoreCase))
    {
        var broker = scope.ServiceProvider.GetRequiredService<IBrokerClient>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var exchangePositions = await broker.GetOpenExchangePositionsAsync();
        var dbPositions = await db.Trades
            .Where(trade => trade.Status == TradeStatus.Executed)
            .ToListAsync();
        var importedPosition = false;

        foreach (var exchangePosition in exchangePositions)
        {
            var clientOrderMarker = exchangePosition.Notes?
                .Split('|', StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(clientOrderMarker) ||
                !clientOrderMarker.StartsWith(
                    "OCO_CLIENT:", StringComparison.Ordinal))
            {
                logger.LogWarning(
                    "Skipping exchange position {Symbol}: its bot OCO marker is missing",
                    exchangePosition.Symbol);
                continue;
            }

            var alreadyTracked = dbPositions.Any(trade =>
                trade.Notes?.Contains(
                    clientOrderMarker, StringComparison.Ordinal) == true);
            if (alreadyTracked)
                continue;

            var ambiguousExistingPosition = dbPositions.Any(trade =>
                trade.Symbol == exchangePosition.Symbol &&
                (trade.Notes == null ||
                 !trade.Notes.Contains("OCO_CLIENT:", StringComparison.Ordinal)));
            if (ambiguousExistingPosition)
            {
                logger.LogWarning(
                    "Exchange position {Symbol} has a bot OCO, but an unmarked DB position already exists; " +
                    "skipping automatic import to avoid duplicating a position",
                    exchangePosition.Symbol);
                continue;
            }

            logger.LogWarning(
                "Importing orphaned bot OCO position {Symbol} from Binance; entry price is estimated",
                exchangePosition.Symbol);
            db.Trades.Add(exchangePosition);
            dbPositions.Add(exchangePosition);
            importedPosition = true;
        }

        if (importedPosition)
            await db.SaveChangesAsync();
    }
}

// --- 10. PIPELINE ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();   // <-- REQUIRED for [Authorize] to work
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.MapHub<TradingHub>("/tradingHub");

app.Run();