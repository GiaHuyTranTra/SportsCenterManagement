using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Auth;

public class LoginRequestAPIViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
