namespace APIViewModel.AuditLog;

public class AuditLogAPIViewModel
{
    public string Id { get; set; } = null!;

    public string? AccountId { get; set; }

    public string? ActorEmail { get; set; }

    public string? ActorFullName { get; set; }

    public string Action { get; set; } = null!;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }
}
