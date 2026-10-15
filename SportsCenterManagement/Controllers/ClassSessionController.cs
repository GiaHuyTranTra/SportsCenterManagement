using System;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassSession;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.ClassSessionService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class ClassSessionController : ControllerBase
{
    private readonly IClassSessionService _sessionService;

    public ClassSessionController(IClassSessionService sessionService)
    {
        _sessionService = sessionService;
    }

    [HttpGet]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetSessionsAsync(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? classId = null,
        [FromQuery] string? coachId = null,
        [FromQuery] int? roomId = null,
        [FromQuery] int? sportId = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        bool isMember = User.IsInRole("Member") && !User.IsInRole("CenterManager") && !User.IsInRole("Receptionist");
        PagedResult<ClassSessionViewDto> result = await _sessionService.GetSessionsPagedAsync(
            from, to, classId, coachId, roomId, sportId, status, page, pageSize, isMember);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize(Roles = "Coach")]
    public async Task<IActionResult> GetMyTeachingScheduleAsync(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        string? coachAccountId = GetActorAccountId();
        if (coachAccountId is null)
        {
            return Unauthorized();
        }

        PagedResult<ClassSessionViewDto> result = await _sessionService.GetCoachScheduleAsync(
            coachAccountId, from, to, status, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetSessionByIdAsync([FromRoute] int id)
    {
        bool isMember = User.IsInRole("Member") && !User.IsInRole("CenterManager") && !User.IsInRole("Receptionist");
        ClassSessionViewDto? result = await _sessionService.GetSessionByIdAsync(id, isMember);
        if (result is null)
        {
            return NotFound(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "SESSION_NOT_FOUND",
                    Message = "Không tìm thấy buổi học."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CreateSessionAsync([FromBody] CreateClassSessionAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassSessionViewDto result = await _sessionService.CreateSessionAsync(actorAccountId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateSessionAsync(
        [FromRoute] int id,
        [FromBody] UpdateClassSessionAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassSessionViewDto result = await _sessionService.UpdateSessionAsync(actorAccountId, id, request);
        return Ok(result);
    }

    [HttpPut("{id:int}/coach")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateSessionCoachAsync(
        [FromRoute] int id,
        [FromBody] UpdateSessionCoachAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassSessionViewDto result = await _sessionService.UpdateSessionCoachAsync(
            actorAccountId, id, request.CoachId);
        return Ok(result);
    }

    [HttpGet("{id:int}/cancel-impact")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> GetSessionCancelImpactAsync([FromRoute] int id)
    {
        SessionCancelImpactViewModel result = await _sessionService.GetSessionCancelImpactAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CancelSessionAsync(
        [FromRoute] int id,
        [FromBody] CancelSessionAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassSessionViewDto result = await _sessionService.CancelSessionAsync(
            actorAccountId, id, request.Reason);
        return Ok(result);
    }

    [HttpGet("{id:int}/registrations")]
    [Authorize(Roles = "CenterManager,Coach")]
    public async Task<IActionResult> GetSessionRegistrationsAsync(
        [FromRoute] int id,
        [FromQuery] bool includeCanceled = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        string? callerAccountId = GetActorAccountId();
        if (callerAccountId is null)
        {
            return Unauthorized();
        }

        bool isManager = User.IsInRole("CenterManager");
        PagedResult<ClassRosterMemberDto> result = await _sessionService.GetSessionRosterAsync(
            id, callerAccountId, isManager, includeCanceled, page, pageSize, search);
        return Ok(result);
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
