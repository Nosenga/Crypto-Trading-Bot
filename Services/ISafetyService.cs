using TradingBot.Models;

namespace TradingBot.Services
{
    /// <summary>
    /// Cross-cutting safety state. The kill switch is in-memory on purpose:
    /// if the app restarts after a panic stop, it comes back with trading
    /// disabled, which is the safe default.
    /// </summary>
    public interface ISafetyService
    {
        bool IsArmed { get; set; }
        bool IsKillSwitchActive { get; }
        DateTime? KillSwitchAt { get; }
        string? KillSwitchReason { get; }
        void ArmTrading();
        void DisarmTrading();
        void ActivateKillSwitch(string reason);
        void DeactivateKillSwitch();
        void UpdateHighWaterMark(Portfolio portfolio);
    }
}