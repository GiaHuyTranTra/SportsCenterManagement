namespace APIViewModel.AccountProfile;

public class AccountProfileAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }

    public string? MemberCode { get; set; }

    public DateTime CreatedAt { get; set; }
}
