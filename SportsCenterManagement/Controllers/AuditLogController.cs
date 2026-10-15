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
        [FromQuery] string? actorId = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null,
        [FromQuery] int? classId = null, [FromQuery] int? sessionId = null,
        [FromQuery] string? search = null)
    {
        if ((from.HasValue && fromDate.HasValue && from != fromDate) ||
            (to.HasValue && toDate.HasValue && to != toDate) ||
            (!string.IsNullOrWhiteSpace(accountId) && !string.IsNullOrWhiteSpace(actorId) && accountId.Trim() != actorId.Trim()))
            throw new Services.ScheduleQueryService.BusinessRuleException("VALIDATION_ERROR", "Các tham số tương đương có giá trị khác nhau.");
        from ??= fromDate;
        to ??= toDate;
        if (to.HasValue && to.Value.TimeOfDay == TimeSpan.Zero)
        {
            to = to.Value.Date.AddDays(1).AddTicks(-1);
        }

        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            return BadRequest("The from date must not be later than the to date.");
        }

        string? effectiveAccountId = string.IsNullOrWhiteSpace(accountId) ? actorId : accountId;

        PagedAuditLogAPIViewModel result = await _auditLogService.GetAuditLogsAsync(
            page,
            pageSize,
            from,
            to,
            effectiveAccountId,
            action,
            entityType,
            entityId, classId, sessionId, search);
        return Ok(result);
    }
}
