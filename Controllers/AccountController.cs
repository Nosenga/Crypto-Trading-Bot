using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TradingBot.Tools;

namespace TradingBot.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;
        private static readonly Dictionary<string, (int Count, DateTime WindowStart)> _loginAttempts = new();
        private static readonly object _attemptsLock = new();
        private const int MaxAttemptsPerMinute = 5;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            // Simple per-IP rate limit: 5 attempts per minute.
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (!TryAcquireLoginSlot(ip))
            {
                ModelState.AddModelError("", "Too many login attempts. Wait a minute and try again.");
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var expectedUser = _configuration["Admin:Username"] ?? "admin";
            var expectedHash = _configuration["Admin:PasswordHash"] ?? "";

            if (string.IsNullOrEmpty(expectedHash))
            {
                ModelState.AddModelError("",
                    "Admin:PasswordHash is not configured. Set it via user-secrets.");
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var userOk = string.Equals(username, expectedUser, StringComparison.Ordinal);
            var passOk = userOk && PasswordHasher.Verify(password ?? "", expectedHash);

            if (!passOk)
            {
                ModelState.AddModelError("", "Invalid username or password.");
                ViewData["ReturnUrl"] = returnUrl;
                return View();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, expectedUser),
                new(ClaimTypes.Role, "Admin")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = false });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        private static bool TryAcquireLoginSlot(string ip)
        {
            lock (_attemptsLock)
            {
                var now = DateTime.UtcNow;
                if (!_loginAttempts.TryGetValue(ip, out var entry) ||
                    (now - entry.WindowStart).TotalMinutes >= 1)
                {
                    _loginAttempts[ip] = (1, now);
                    return true;
                }
                if (entry.Count >= MaxAttemptsPerMinute)
                    return false;
                _loginAttempts[ip] = (entry.Count + 1, entry.WindowStart);
                return true;
            }
        }
    }
}