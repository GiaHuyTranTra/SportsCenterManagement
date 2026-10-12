using System.Security.Claims;
using APIViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScheduleQueryController : ControllerBase
{
    private readonly IScheduleQueryService _service;

    public ScheduleQueryController(IScheduleQueryService service)
    {
        _service = service;
    }

    [HttpGet("center")]
    [Authorize(Roles = "CenterManager,Receptionist")]
    public async Task<IActionResult> GetCenterSchedule(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? sportId = null,
        [FromQuery] string? coachId = null,
        [FromQuery] int? roomId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        PagedResult<ClassSessionViewDto> result = await _service.GetCenterScheduleAsync(
            from, to, sportId, coachId, roomId, status, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("available")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailableSessions(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? sportId = null,
        [FromQuery] string? coachId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        PagedResult<ClassSessionViewDto> result = await _service.GetAvailableSessionsAsync(
            from, to, sportId, coachId, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("my-classes")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyClassSchedule(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "UNAUTHORIZED", message = "Không xác định được Member token" }
            });
        }

        List<MemberBookedSessionDto> result = await _service.GetMyClassScheduleAsync(memberId, from, to);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("coach-teaching")]
    [Authorize(Roles = "Coach")]
    public async Task<IActionResult> GetCoachTeachingSchedule(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        string? coachId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(coachId))
        {
            return Unauthorized(new
            {
                success = false,
                error = new { code = "UNAUTHORIZED", message = "Không xác định được Coach token" }
            });
        }

        PagedResult<ClassSessionViewDto> result = await _service.GetCoachTeachingScheduleAsync(
            coachId, from, to, status, page, pageSize);
        return Ok(new { success = true, data = result });
    }

    [HttpGet("sessions/{sessionId:int}/roster")]
    [Authorize(Roles = "Coach,CenterManager")]
    public async Task<IActionResult> GetSessionRoster(
        int sessionId,
        [FromQuery] bool includeCanceled = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        string callerId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        bool isManager = User.IsInRole("CenterManager");

        try
        {
            PagedResult<ClassRosterMemberDto> result = await _service.GetSessionRosterAsync(
                sessionId, callerId, isManager, includeCanceled, page, pageSize);
            return Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException exception)
        {
            int statusCode = exception.Code switch
            {
                "FORBIDDEN" => StatusCodes.Status403Forbidden,
                "SESSION_NOT_FOUND" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest
            };

            return StatusCode(statusCode, new
            {
                success = false,
                error = new { code = exception.Code, message = exception.Message },
                traceId = HttpContext.TraceIdentifier
            });
        }
    }
}
