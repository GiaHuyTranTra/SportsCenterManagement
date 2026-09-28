namespace SportsCenterManagement.DTOs.AuditLog;

public class AuditLogQueryDto
{
	public string? AccountId { get; set; }
	public string? Action { get; set; }
	public DateTime? FromDate { get; set; }
	public DateTime? ToDate { get; set; }
	public int PageIndex { get; set; } = 1;
	public int PageSize { get; set; } = 20;
}
