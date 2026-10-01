using System;
using System.Threading.Tasks;
using APIViewModel.AuditLog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuditLogService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class AuditLogController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet]
    public async Task<IActionResult> GetAuditLogsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        PagedAuditLogResultAPIViewModel result = await _auditLogService.GetAuditLogsAsync(
            page,
            pageSize,
            search,
            action,
            entityType,
            fromDate,
            toDate);

        return Ok(result);
    }
}
