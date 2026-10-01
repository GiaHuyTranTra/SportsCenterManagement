using APIViewModel.Coach;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CoachService;
using SportsCenterManagement.Filter;
using System.Security.Claims;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
[TypeFilter(typeof(AuthFilter))]
public class CoachController : ControllerBase
{
    private readonly ICoachService _coachService;

    public CoachController(ICoachService coachService)
    {
        _coachService = coachService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCoachesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int? disciplineId = null)
    {
        if (status is not null &&
            !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Status must be Active or Inactive.");
        }

        PagedCoachAPIViewModel result = await _coachService.GetCoachesAsync(
            page,
            pageSize,
            search,
            status,
            disciplineId);
        return Ok(result);
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetCoachByIdAsync([FromRoute] string accountId)
    {
        CoachDetailAPIViewModel? result = await _coachService.GetCoachByIdAsync(accountId);
        return result is null ? NotFound("Coach not found.") : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCoachAsync(
        [FromBody] CreateManagedCoachAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (CreateCoachResult Result, CoachDetailAPIViewModel? Data) result =
            await _coachService.CreateCoachAsync(actorAccountId, request);
        return result.Result switch
        {
            CreateCoachResult.Success when result.Data is not null =>
                StatusCode(StatusCodes.Status201Created, result.Data),
            CreateCoachResult.InvalidData => BadRequest("Invalid coach data."),
            CreateCoachResult.DuplicateEmail => Conflict("Email already exists."),
            CreateCoachResult.DuplicatePhone => Conflict("Phone number already exists."),
            CreateCoachResult.DisciplineNotFoundOrInactive =>
                BadRequest("A discipline was not found or is inactive."),
            CreateCoachResult.CoachRoleMissing => StatusCode(StatusCodes.Status500InternalServerError),
            CreateCoachResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateCoachAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateManagedCoachAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (UpdateCoachResult Result, CoachDetailAPIViewModel? Data) result =
            await _coachService.UpdateCoachAsync(actorAccountId, accountId, request);
        return result.Result switch
        {
            UpdateCoachResult.Success when result.Data is not null => Ok(result.Data),
            UpdateCoachResult.NotFound => NotFound("Coach not found."),
            UpdateCoachResult.InvalidData => BadRequest("Invalid coach data."),
            UpdateCoachResult.DuplicatePhone => Conflict("Phone number already exists."),
            UpdateCoachResult.DisciplineNotFoundOrInactive =>
                BadRequest("A discipline was not found or is inactive."),
            UpdateCoachResult.NoChanges => BadRequest("No changes were provided."),
            UpdateCoachResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPatch("{accountId}/status")]
    public async Task<IActionResult> UpdateCoachStatusAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateCoachStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        UpdateCoachStatusResult result = await _coachService.UpdateCoachStatusAsync(
            actorAccountId,
            accountId,
            request.Status);
        return result switch
        {
            UpdateCoachStatusResult.Success => Ok(),
            UpdateCoachStatusResult.NotFound => NotFound("Coach not found."),
            UpdateCoachStatusResult.InvalidStatus => BadRequest("Status must be Active or Inactive."),
            UpdateCoachStatusResult.NoChanges => BadRequest("No status change was provided."),
            UpdateCoachStatusResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{accountId}")]
    public async Task<IActionResult> SoftDeleteCoachAsync([FromRoute] string accountId)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        DeleteCoachResult result = await _coachService.SoftDeleteCoachAsync(actorAccountId, accountId);
        return result switch
        {
            DeleteCoachResult.Success => NoContent(),
            DeleteCoachResult.NotFound => NotFound("Coach not found."),
            DeleteCoachResult.AlreadyDeleted => BadRequest("Coach is already deleted."),
            DeleteCoachResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
