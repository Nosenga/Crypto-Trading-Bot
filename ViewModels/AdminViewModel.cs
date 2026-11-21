namespace TradingBot.ViewModels;

/// <summary>
/// View model for the Admin Kill Switch page.
/// </summary>
public class AdminViewModel
{
    public bool IsArmed { get; set; }
    public bool IsKillSwitchActive { get; set; }
    public DateTime? KillSwitchAt { get; set; }
    public string? KillSwitchReason { get; set; }
    public decimal HighWaterMark { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal StartOfDayBalance { get; set; }
    public decimal DailyLossLimitPercent { get; set; }
    public decimal DailyLossPercent { get; set; }
    public bool IsDailyLossLimitBreached { get; set; }
}