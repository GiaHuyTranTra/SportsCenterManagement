using System.Collections.Generic;

namespace APIViewModel.AuditLog;

public class PagedAuditLogResultAPIViewModel
{
    public List<AuditLogListItemAPIViewModel> Items { get; set; } = new List<AuditLogListItemAPIViewModel>();

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }
}
