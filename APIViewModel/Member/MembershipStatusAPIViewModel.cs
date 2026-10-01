namespace APIViewModel.Member;

public class MembershipStatusAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public int RemainingDays { get; set; }

    public bool ExpiringSoon { get; set; }

    public int? SubscriptionId { get; set; }

    public int? PackageId { get; set; }

    public string? PackageName { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? SuspensionReason { get; set; }

    public int? UpcomingSubscriptionId { get; set; }

    public string? UpcomingPackageName { get; set; }

    public DateOnly? UpcomingStartDate { get; set; }

    public DateOnly? UpcomingEndDate { get; set; }
}
