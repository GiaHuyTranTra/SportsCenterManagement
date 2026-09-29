namespace APIViewModel.MembershipPackage;

public class PagedMembershipPackageResultAPIViewModel
{
    public List<MembershipPackageListItemAPIViewModel> Items { get; set; }
        = new List<MembershipPackageListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
