namespace APIViewModel.Member;

public class MemberSearchAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string AccountStatus { get; set; } = null!;

    public string? PackageName { get; set; }

    public string MembershipStatus { get; set; } = null!;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? DaysRemaining { get; set; }

    public bool IsExpiringSoon { get; set; }
}
