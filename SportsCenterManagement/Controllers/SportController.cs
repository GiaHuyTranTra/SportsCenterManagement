using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Common;
using APIViewModel.Discipline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.DisciplineService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class SportController : ControllerBase
{
    private readonly IDisciplineService _disciplineService;

    public SportController(IDisciplineService disciplineService)
    {
        _disciplineService = disciplineService;
    }

    [HttpGet]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetSportsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        PagedResult<DisciplineAPIViewModel> result = await _disciplineService.GetSportsPagedAsync(
            page, pageSize, search, isActive);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CreateSportAsync([FromBody] CreateDisciplineAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (CreateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await _disciplineService.CreateDisciplineAsync(actorAccountId, request, sportContract: true);

        return result.Result switch
        {
            CreateDisciplineResult.Success when result.Data is not null =>
                StatusCode(StatusCodes.Status201Created, result.Data),
            CreateDisciplineResult.InvalidData => BadRequest(CreateError("VALIDATION_ERROR", "Dữ liệu bộ môn không hợp lệ.")),
            CreateDisciplineResult.DuplicateName => Conflict(CreateError(
                "SPORT_NAME_DUPLICATE",
                "Tên bộ môn đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên bộ môn đã tồn tại trong hệ thống." } } })),
            CreateDisciplineResult.ConcurrencyConflict => Conflict(CreateError("CONCURRENCY_CONFLICT", "Xung đột dữ liệu đồng thời.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateError("INTERNAL_SERVER_ERROR", "Lỗi hệ thống."))
        };
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateSportAsync(
        [FromRoute] int id,
        [FromBody] UpdateDisciplineAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        (UpdateDisciplineResult Result, DisciplineAPIViewModel? Data) result =
            await _disciplineService.UpdateDisciplineAsync(actorAccountId, id, request, sportContract: true);

        return result.Result switch
        {
            UpdateDisciplineResult.Success when result.Data is not null => Ok(result.Data),
            UpdateDisciplineResult.NotFound => NotFound(CreateError("SPORT_NOT_FOUND", "Không tìm thấy bộ môn.")),
            UpdateDisciplineResult.InvalidData => BadRequest(CreateError("VALIDATION_ERROR", "Dữ liệu bộ môn không hợp lệ.")),
            UpdateDisciplineResult.DuplicateName => Conflict(CreateError(
                "SPORT_NAME_DUPLICATE",
                "Tên bộ môn đã tồn tại.",
                new Dictionary<string, string[]> { { "name", new[] { "Tên bộ môn đã tồn tại trong hệ thống." } } })),
            UpdateDisciplineResult.NoChanges => Ok(result.Data),
            UpdateDisciplineResult.ConcurrencyConflict => Conflict(CreateError("CONCURRENCY_CONFLICT", "Xung đột dữ liệu đồng thời.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateError("INTERNAL_SERVER_ERROR", "Lỗi hệ thống."))
        };
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateSportStatusAsync(
        [FromRoute] int id,
        [FromBody] UpdateDisciplineStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        if (!request.IsActive.HasValue)
        {
            return BadRequest(CreateError("VALIDATION_ERROR", "Trạng thái không được để trống."));
        }

        UpdateDisciplineStatusResult result = await _disciplineService.UpdateDisciplineStatusAsync(
            actorAccountId, id, request.IsActive.Value, sportContract: true);

        return result switch
        {
            UpdateDisciplineStatusResult.Success => Ok(new { id = id, isActive = request.IsActive.Value }),
            UpdateDisciplineStatusResult.NotFound => NotFound(CreateError("SPORT_NOT_FOUND", "Không tìm thấy bộ môn.")),
            UpdateDisciplineStatusResult.NoChanges => Ok(new { id = id, isActive = request.IsActive.Value }),
            UpdateDisciplineStatusResult.ConcurrencyConflict => Conflict(CreateError("CONCURRENCY_CONFLICT", "Xung đột dữ liệu đồng thời.")),
            _ => StatusCode(StatusCodes.Status500InternalServerError, CreateError("INTERNAL_SERVER_ERROR", "Lỗi hệ thống."))
        };
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }

    private ApiErrorResponseAPIViewModel CreateError(string code, string message, object? details = null)
    {
        return new ApiErrorResponseAPIViewModel
        {
            Success = false,
            Error = new ApiErrorAPIViewModel
            {
                Code = code,
                Message = message,
                Details = details
            },
            TraceId = HttpContext.TraceIdentifier
        };
    }
}
