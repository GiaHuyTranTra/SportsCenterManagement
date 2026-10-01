using System.Security.Claims;
using APIViewModel.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.ReceptionistService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
[TypeFilter(typeof(AuthFilter))]
public class ReceptionistController : ControllerBase
{
    private readonly IReceptionistService _receptionistService;

    public ReceptionistController(IReceptionistService receptionistService)
    {
        _receptionistService = receptionistService;
    }

    [HttpGet]
    public async Task<IActionResult> GetReceptionistsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? workShift = null)
    {
        if (status is not null &&
            !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Status must be Active or Inactive.");
        }

        PagedReceptionistAPIViewModel result = await _receptionistService.GetReceptionistsAsync(
            page,
            pageSize,
            search,
            status,
            workShift);
        return Ok(result);
    }

    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetReceptionistByIdAsync([FromRoute] string accountId)
    {
        ReceptionistDetailAPIViewModel? result =
            await _receptionistService.GetReceptionistByIdAsync(accountId);
        return result is null ? NotFound("Receptionist not found.") : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateReceptionistAsync(
        [FromBody] CreateReceptionistAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result =
            await _receptionistService.CreateReceptionistAsync(actorAccountId, request);
        return MapCreateResult(result);
    }

    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateReceptionistAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateManagedReceptionistAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (UpdateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result =
            await _receptionistService.UpdateReceptionistAsync(actorAccountId, accountId, request);
        return result.Result switch
        {
            UpdateReceptionistResult.Success when result.Data is not null => Ok(result.Data),
            UpdateReceptionistResult.NotFound => NotFound("Receptionist not found."),
            UpdateReceptionistResult.InvalidData => BadRequest("Invalid receptionist data."),
            UpdateReceptionistResult.DuplicatePhone => Conflict("Phone number already exists."),
            UpdateReceptionistResult.NoChanges => BadRequest("No changes were provided."),
            UpdateReceptionistResult.ConcurrencyConflict =>
                Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPatch("{accountId}/status")]
    public async Task<IActionResult> UpdateReceptionistStatusAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateReceptionistStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        UpdateReceptionistStatusResult result =
            await _receptionistService.UpdateReceptionistStatusAsync(
                actorAccountId,
                accountId,
                request.Status);
        return result switch
        {
            UpdateReceptionistStatusResult.Success => Ok(),
            UpdateReceptionistStatusResult.NotFound => NotFound("Receptionist not found."),
            UpdateReceptionistStatusResult.InvalidStatus =>
                BadRequest("Status must be Active or Inactive."),
            UpdateReceptionistStatusResult.NoChanges =>
                BadRequest("No status change was provided."),
            UpdateReceptionistStatusResult.ConcurrencyConflict =>
                Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpDelete("{accountId}")]
    public async Task<IActionResult> SoftDeleteReceptionistAsync([FromRoute] string accountId)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        DeleteReceptionistResult result =
            await _receptionistService.SoftDeleteReceptionistAsync(actorAccountId, accountId);
        return result switch
        {
            DeleteReceptionistResult.Success => NoContent(),
            DeleteReceptionistResult.NotFound => NotFound("Receptionist not found."),
            DeleteReceptionistResult.AlreadyDeleted =>
                BadRequest("Receptionist is already deleted."),
            DeleteReceptionistResult.ConcurrencyConflict =>
                Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private IActionResult MapCreateResult(
        (CreateReceptionistResult Result, ReceptionistDetailAPIViewModel? Data) result)
    {
        return result.Result switch
        {
            CreateReceptionistResult.Success when result.Data is not null =>
                StatusCode(StatusCodes.Status201Created, result.Data),
            CreateReceptionistResult.InvalidData => BadRequest("Invalid receptionist data."),
            CreateReceptionistResult.DuplicateEmail => Conflict("Email already exists."),
            CreateReceptionistResult.DuplicatePhone => Conflict("Phone number already exists."),
            CreateReceptionistResult.ReceptionistRoleMissing =>
                StatusCode(StatusCodes.Status500InternalServerError),
            CreateReceptionistResult.ConcurrencyConflict =>
                Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
