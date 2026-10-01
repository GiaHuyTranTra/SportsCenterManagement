using APIViewModel.AuditLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuditLogService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
[TypeFilter(typeof(AuthFilter))]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? accountId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest("The from date must not be later than the to date.");
        }

        PagedAuditLogAPIViewModel result = await _auditLogService.GetAuditLogsAsync(
            page,
            pageSize,
            from,
            to,
            accountId,
            action,
            entityType);
        return Ok(result);
    }
}
