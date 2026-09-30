namespace APIViewModel.Account;

public class CurrentUserProfileAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public string Role { get; set; } = null!;

    public string? FullName { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? MemberCode { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }

    public string? WorkShift { get; set; }
}
