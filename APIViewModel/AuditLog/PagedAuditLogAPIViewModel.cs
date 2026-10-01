namespace APIViewModel.AuditLog;

public class PagedAuditLogAPIViewModel
{
    public List<AuditLogAPIViewModel> Items { get; set; }
        = new List<AuditLogAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
