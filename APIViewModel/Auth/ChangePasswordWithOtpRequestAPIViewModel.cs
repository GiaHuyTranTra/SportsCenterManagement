using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Auth;

public class ChangePasswordWithOtpRequestAPIViewModel
{
    [Required]
    [MaxLength(100)]
    public string CurrentPassword { get; set; } = null!;

    [Required]
    [RegularExpression("^[0-9]{6}$")]
    public string Otp { get; set; } = null!;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string NewPassword { get; set; } = null!;

    [Required]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = null!;
}
