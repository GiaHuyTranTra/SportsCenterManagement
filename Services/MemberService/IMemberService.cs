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

public enum CreateManagedMemberResult
{
    Success,
    DuplicateEmail,
    DuplicatePhone,
    InvalidData,
    MemberRoleMissing
}

public enum DeleteMemberResult
{
    Success,
    NotFound,
    AlreadyDeleted
}

public interface IMemberService
{
    Task<PagedMemberResultAPIViewModel> GetMembersAsync(
        int page,
        int pageSize,
        string? search,
        string? status);

    Task<MemberDetailAPIViewModel?> GetMemberByIdAsync(string accountId);

    Task<(CreateManagedMemberResult Result, CreateManagedMemberResponseAPIViewModel? Data)>
        CreateManagedMemberAsync(CreateManagedMemberAPIViewModel request);

    Task<DeleteMemberResult> SoftDeleteMemberAsync(string accountId);

    Task<UpdateMemberResult> UpdateMemberAsync(
        string accountId,
        UpdateMemberAPIViewModel request);

    Task<UpdateMemberStatusResult> UpdateMemberStatusAsync(
        string accountId,
        string status);

    Task<List<MemberSearchAPIViewModel>> QuickSearchMembersAsync(string keyword);

    Task<List<MembershipStatusAPIViewModel>> GetMembershipStatusesAsync(
        string? search,
        string? filter);
}
