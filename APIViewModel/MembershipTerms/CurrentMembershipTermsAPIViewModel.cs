namespace APIViewModel.MembershipTerms;

public sealed class CurrentMembershipTermsAPIViewModel
{
    public string Version { get; set; } = null!;
    public string Title { get; set; } = null!;
    public DateOnly EffectiveFrom { get; set; }
    public string ContentHash { get; set; } = null!;
    public List<MembershipTermsSectionAPIViewModel> Sections { get; set; } = new();
    public List<MembershipTermsAcknowledgementAPIViewModel> RequiredAcknowledgements { get; set; } = new();
    public bool Accepted { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
}

public sealed class MembershipTermsSectionAPIViewModel
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;
}

public sealed class MembershipTermsAcknowledgementAPIViewModel
{
    public string Id { get; set; } = null!;
    public string Label { get; set; } = null!;
}
