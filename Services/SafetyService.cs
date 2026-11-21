using TradingBot.Models;

namespace TradingBot.Services
{
    public class SafetyService : ISafetyService
    {
        private readonly object _lock = new();
        private volatile bool _active;
        private volatile bool _armed;
        private DateTime? _at;
        private string? _reason;

        public bool IsArmed
        {
            get => _armed;
            set => _armed = value;
        }

        public bool IsKillSwitchActive => _active;
        public DateTime? KillSwitchAt => _at;
        public string? KillSwitchReason => _reason;

        public void ArmTrading() => IsArmed = true;

        public void DisarmTrading() => IsArmed = false;

        public void UpdateHighWaterMark(Portfolio portfolio)
        {
            if (portfolio.TotalEquity > portfolio.HighWaterMark)
                portfolio.HighWaterMark = portfolio.TotalEquity;
        }

        public void ActivateKillSwitch(string reason)
        {
            lock (_lock)
            {
                _active = true;
                _at = DateTime.UtcNow;
                _reason = reason;
            }
        }

        public void DeactivateKillSwitch()
        {
            lock (_lock)
            {
                _active = false;
                _at = null;
                _reason = null;
            }
        }
    }
}