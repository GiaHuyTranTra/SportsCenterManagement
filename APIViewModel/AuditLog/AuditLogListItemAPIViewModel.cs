using System;

namespace APIViewModel.AuditLog;

public class AuditLogListItemAPIViewModel
{
    public string Id { get; set; } = null!;

    public string? AccountId { get; set; }

    public string? AccountName { get; set; }

    public string Action { get; set; } = null!;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
