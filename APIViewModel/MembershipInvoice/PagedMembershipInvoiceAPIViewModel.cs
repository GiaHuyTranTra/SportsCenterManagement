namespace APIViewModel.MembershipInvoice;

public class PagedMembershipInvoiceAPIViewModel
{
    public List<MembershipReceiptAPIViewModel> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}
