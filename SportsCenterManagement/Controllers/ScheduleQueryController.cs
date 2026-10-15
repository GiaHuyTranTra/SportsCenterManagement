using System.Security.Claims;
using APIViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ScheduleQueryService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
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
        return Ok(result);
    }

    [HttpGet("available")]
    [Authorize(Roles = "CenterManager,Receptionist,Coach,Member")]
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
        return Ok(result);
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
            return Unauthorized();
        }

        List<MemberBookedSessionDto> result = await _service.GetMyClassScheduleAsync(memberId, from, to);
        return Ok(result);
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
            return Unauthorized();
        }

        PagedResult<ClassSessionViewDto> result = await _service.GetCoachTeachingScheduleAsync(
            coachId, from, to, status, page, pageSize);
        return Ok(result);
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

        PagedResult<ClassRosterMemberDto> result = await _service.GetSessionRosterAsync(
            sessionId, callerId, isManager, includeCanceled, page, pageSize);
        return Ok(result);
    }
}
