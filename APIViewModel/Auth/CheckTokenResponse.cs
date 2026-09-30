namespace APIViewModel.Auth;

public class CheckTokenResponse
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; }
}
