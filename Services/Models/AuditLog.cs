namespace Services.Models;

public class AuditLog
{
	public string Id { get; set; } = null!;
	public string? AccountId { get; set; }
	public string Action { get; set; } = string.Empty;
	public string? EntityType { get; set; }
	public string? EntityId { get; set; }
	public string? Description { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
