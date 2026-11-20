using System.ComponentModel.DataAnnotations;

namespace TradingBot.ViewModels;

/// <summary>
/// View model for the Admin login page.
/// </summary>
public class LoginViewModel
{
    [Required]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}