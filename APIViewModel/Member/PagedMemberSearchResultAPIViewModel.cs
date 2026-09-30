namespace APIViewModel.Member;

public class PagedMemberSearchResultAPIViewModel
{
    public List<MemberSearchAPIViewModel> Items { get; set; }
        = new List<MemberSearchAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
