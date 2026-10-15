using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Auth;

public class RequestLoginEmailVerificationAPIViewModel
{
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string Password { get; set; } = null!;
}

public class RequestRegistrationEmailVerificationAPIViewModel
{
    [Required]
    [EmailAddress]
    [MaxLength(150)]
    public string Email { get; set; } = null!;
}

public class EmailVerificationOtpResponseAPIViewModel
{
    public string Message { get; set; } = null!;

    public int ExpiresInSeconds { get; set; }

    public int CooldownSeconds { get; set; }

    public string? DemoCode { get; set; }
}
