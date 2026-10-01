using System;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.AuditLogService;
using Services.CoachService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class CoachController : ControllerBase
{
    private readonly ICoachService _coachService;
    private readonly IAuditLogService _auditLogService;

    public CoachController(
        ICoachService coachService,
        IAuditLogService auditLogService)
    {
        _coachService = coachService;
        _auditLogService = auditLogService;
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet]
    public async Task<IActionResult> GetCoachesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            string trimmedStatus = status.Trim();
            if (!string.Equals(trimmedStatus, "Active", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(trimmedStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid status filter. Allowed values: Active, Inactive.");
            }
        }

        PagedCoachResultAPIViewModel result = await _coachService.GetCoachesAsync(page, pageSize, search, status);
        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetCoachByIdAsync([FromRoute] string accountId)
    {
        CoachDetailAPIViewModel? result = await _coachService.GetCoachByIdAsync(accountId);
        if (result is null)
        {
            return NotFound("Coach not found.");
        }

        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateCoachAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateCoachAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            bool isUpdated = await _coachService.UpdateCoachAsync(accountId, request);
            if (!isUpdated)
            {
                return NotFound("Coach not found.");
            }

            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _auditLogService.RecordAsync(
                currentUserId,
                "UPDATE",
                "COACH",
                accountId,
                $"Cập nhật hồ sơ huấn luyện viên {request.FullName}.");

            return Ok("Update coach successful.");
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}/status")]
    public async Task<IActionResult> UpdateCoachStatusAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateCoachStatusAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        bool isUpdated = await _coachService.UpdateCoachStatusAsync(accountId, request);
        if (!isUpdated)
        {
            return NotFound("Coach not found.");
        }

        string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        await _auditLogService.RecordAsync(
            currentUserId,
            request.Status.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "ACTIVATE" : "DEACTIVATE",
            "COACH",
            accountId,
            $"Cập nhật trạng thái huấn luyện viên sang {request.Status}.");

        return Ok("Update coach status successful.");
    }
}
