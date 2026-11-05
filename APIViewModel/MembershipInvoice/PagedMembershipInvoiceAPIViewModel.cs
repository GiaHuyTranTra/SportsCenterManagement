namespace APIViewModel.MembershipInvoice;

public class PagedMembershipInvoiceAPIViewModel
{
    public List<MembershipReceiptAPIViewModel> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);

    /// <summary>
    /// Totals over every invoice the filters match, not just the page in
    /// <see cref="Items"/>. The counter screens show these as centre-wide
    /// figures, so computing them from one page understates them.
    /// </summary>
    public MembershipInvoiceSummaryAPIViewModel Summary { get; set; } = new();
}

public class MembershipInvoiceSummaryAPIViewModel
{
    public int PaidCount { get; set; }
    public decimal PaidAmount { get; set; }
    public int PendingCount { get; set; }
    public decimal PendingAmount { get; set; }
    public int CanceledCount { get; set; }
    public decimal CanceledAmount { get; set; }
}
