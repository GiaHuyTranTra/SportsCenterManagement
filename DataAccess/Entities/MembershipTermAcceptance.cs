namespace DataAccess.Entities;

public sealed class MembershipTermAcceptance
{
    public int Id { get; set; }

    public string MemberId { get; set; } = null!;

    public string TermsVersion { get; set; } = null!;

    public string ContentHash { get; set; } = null!;

    public string AcceptanceChannel { get; set; } = null!;

    public DateTime AcceptedAtUtc { get; set; }

    public Member Member { get; set; } = null!;
}
