using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using SportsCenterManagement.DTOs.AuditLog;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
public class AuditLogController : ControllerBase
{
	private readonly IAuditLogService _auditLogService;

	public AuditLogController(IAuditLogService auditLogService) => _auditLogService = auditLogService;

	[HttpGet]
	public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryDto query)
	{
		var result = await _auditLogService.GetLogsAsync(query);
		return Ok(new { success = true, data = result });
	}
}
