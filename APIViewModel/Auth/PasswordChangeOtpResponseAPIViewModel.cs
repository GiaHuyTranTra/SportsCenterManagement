namespace APIViewModel.Auth;

public class PasswordChangeOtpResponseAPIViewModel
{
    public string Message { get; set; } = null!;

    public int ExpiresInSeconds { get; set; }

    public int CooldownSeconds { get; set; }
}
