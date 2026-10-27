namespace APIViewModel.Report;

public class MemberReportAPIViewModel
{
    public DateOnly? From { get; set; }
    public DateOnly To { get; set; }
    public DateOnly AsOfDate { get; set; }
    public string TimeZoneId { get; set; } = null!;
    public int TotalMembers { get; set; }
    public int NewMembers { get; set; }
    public int ActiveMembers { get; set; }
    public int ExpiredMembers { get; set; }
    public int SuspendedMembers { get; set; }
    public int UpcomingMembers { get; set; }
    public int PendingPaymentMembers { get; set; }
    public int MembersWithoutSubscription { get; set; }
    public int InactiveAccounts { get; set; }
}
