using APIViewModel.MembershipTerms;

namespace Services.MembershipTermsService;

public enum AcceptMembershipTermsResult
{
    Success,
    VersionMismatch,
    MemberNotEligible,
    MemberNotFound
}

public interface IMembershipTermsService
{
    Task<CurrentMembershipTermsAPIViewModel> GetCurrentAsync(string memberId);

    Task<AcceptMembershipTermsResult> AcceptCurrentAsync(
        string memberId,
        string version,
        string contentHash);

    Task<MemberTermsStatusAPIViewModel> GetMemberStatusAsync(string memberId);
}
