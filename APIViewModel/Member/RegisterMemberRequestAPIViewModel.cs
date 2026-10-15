using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Member;

public class RegisterMemberRequestAPIViewModel
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [MaxLength(150, ErrorMessage = "Email must not exceed 150 characters.")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [MaxLength(100, ErrorMessage = "Password must not exceed 100 characters.")]
    public string Password { get; set; } = null!;

    [Required(ErrorMessage = "Confirm password is required.")]
    [Compare(nameof(Password), ErrorMessage = "Password and confirm password do not match.")]
    public string ConfirmPassword { get; set; } = null!;

    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Email verification code must contain 6 digits.")]
    public string? EmailVerificationCode { get; set; }
}
