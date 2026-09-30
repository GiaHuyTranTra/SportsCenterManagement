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

    Task<PagedMemberSearchResultAPIViewModel> QuickSearchMembersAsync(
        string keyword,
        int page,
        int pageSize);
}
