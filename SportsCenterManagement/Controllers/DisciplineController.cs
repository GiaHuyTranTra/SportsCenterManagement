using APIViewModel.Discipline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DisciplineService;
using SportsCenterManagement.Filter;
using System.Security.Claims;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
[TypeFilter(typeof(AuthFilter))]
public class DisciplineController : ControllerBase
{
    private readonly IDisciplineService _disciplineService;

    public DisciplineController(IDisciplineService disciplineService)
    {
        _disciplineService = disciplineService;
    }

    [HttpGet]
    public async Task<IActionResult> GetDisciplinesAsync([FromQuery] bool activeOnly = true)
    {
        List<DisciplineAPIViewModel> result =
            await _disciplineService.GetDisciplinesAsync(activeOnly);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDisciplineAsync(
        [FromBody] CreateDisciplineAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (CreateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await _disciplineService.CreateDisciplineAsync(actorAccountId, request);
        return result.Result switch
        {
            CreateDisciplineResult.Success when result.Data is not null =>
                StatusCode(StatusCodes.Status201Created, result.Data),
            CreateDisciplineResult.InvalidData => BadRequest("Invalid discipline data."),
            CreateDisciplineResult.DuplicateName => Conflict("Discipline name already exists."),
            CreateDisciplineResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPatch("{disciplineId:int}")]
    public async Task<IActionResult> UpdateDisciplineAsync(
        [FromRoute] int disciplineId,
        [FromBody] UpdateDisciplineAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (UpdateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await _disciplineService.UpdateDisciplineAsync(actorAccountId, disciplineId, request);
        return result.Result switch
        {
            UpdateDisciplineResult.Success when result.Data is not null => Ok(result.Data),
            UpdateDisciplineResult.NotFound => NotFound("Discipline not found."),
            UpdateDisciplineResult.InvalidData => BadRequest("Invalid discipline data."),
            UpdateDisciplineResult.DuplicateName => Conflict("Discipline name already exists."),
            UpdateDisciplineResult.NoChanges => BadRequest("No changes were provided."),
            UpdateDisciplineResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    [HttpPatch("{disciplineId:int}/status")]
    public async Task<IActionResult> UpdateDisciplineStatusAsync(
        [FromRoute] int disciplineId,
        [FromBody] UpdateDisciplineStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null || !request.IsActive.HasValue)
        {
            return actorAccountId is null ? Unauthorized() : BadRequest();
        }

        UpdateDisciplineStatusResult result = await _disciplineService.UpdateDisciplineStatusAsync(
            actorAccountId,
            disciplineId,
            request.IsActive.Value);
        return result switch
        {
            UpdateDisciplineStatusResult.Success => Ok(),
            UpdateDisciplineStatusResult.NotFound => NotFound("Discipline not found."),
            UpdateDisciplineStatusResult.NoChanges => BadRequest("No status change was provided."),
            UpdateDisciplineStatusResult.ConcurrencyConflict => Conflict("A concurrent request conflict occurred."),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
