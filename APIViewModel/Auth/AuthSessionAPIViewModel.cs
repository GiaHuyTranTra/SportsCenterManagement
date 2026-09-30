namespace APIViewModel.Auth;

public class AuthSessionAPIViewModel
{
    public string AccessToken { get; set; } = null!;

    public string TokenType { get; set; } = "Bearer";

    public DateTime ExpiresAtUtc { get; set; }

    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; }
}
