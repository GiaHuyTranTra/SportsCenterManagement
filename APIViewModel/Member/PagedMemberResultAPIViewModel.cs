using System.Collections.Generic;

namespace APIViewModel.Member;

public class PagedMemberResultAPIViewModel
{
    public List<MemberListItemAPIViewModel> Items { get; set; } = new List<MemberListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
