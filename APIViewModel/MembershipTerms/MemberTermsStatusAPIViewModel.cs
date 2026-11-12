namespace APIViewModel.MembershipTerms;

public sealed class MemberTermsStatusAPIViewModel
{
    public string MemberId { get; set; } = null!;
    public string CurrentVersion { get; set; } = null!;
    public bool Accepted { get; set; }
    public string? AcceptedVersion { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}
