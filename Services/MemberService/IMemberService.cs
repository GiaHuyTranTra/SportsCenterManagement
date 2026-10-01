using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewModel.Member;

namespace Services.MemberService;

public enum UpdateMemberResult
{
    Success,
    NotFound,
    DuplicatePhone,
    InvalidData,
    NoChanges
}

public enum UpdateMemberStatusResult
{
    Success,
    NotFound,
    InvalidStatus
}

public enum DeleteMemberResult
{
    Success,
    NotFound,
    /// The member has subscriptions or invoices, so the row cannot be removed
    /// without destroying financial history. Callers must deactivate instead.
    HasMembershipHistory
}

public interface IMemberService
{
    Task<PagedMemberResultAPIViewModel> GetMembersAsync(
        int page,
        int pageSize,
        string? search,
        string? status);

    Task<MemberDetailAPIViewModel?> GetMemberByIdAsync(string accountId);

    Task<UpdateMemberResult> UpdateMemberAsync(
        string accountId,
        UpdateMemberAPIViewModel request);

    Task<UpdateMemberStatusResult> UpdateMemberStatusAsync(
        string accountId,
        string status);

    Task<DeleteMemberResult> DeleteMemberAsync(string accountId);

    Task<List<MemberSearchAPIViewModel>> QuickSearchMembersAsync(string keyword);
}
