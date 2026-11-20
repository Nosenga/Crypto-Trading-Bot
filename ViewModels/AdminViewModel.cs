namespace TradingBot.ViewModels;

/// <summary>
/// View model for the Admin Kill Switch page.
/// </summary>
public class AdminViewModel
{
    public bool IsKillSwitchActive { get; set; }
    public DateTime? KillSwitchAt { get; set; }
    public string? KillSwitchReason { get; set; }
}