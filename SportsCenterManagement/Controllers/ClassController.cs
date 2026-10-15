using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Class;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.ClassService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class ClassController : ControllerBase
{
    private readonly IClassService _classService;

    public ClassController(IClassService classService)
    {
        _classService = classService;
    }

    [HttpGet]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetClassesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] int? sportId = null,
        [FromQuery] string? status = null)
    {
        bool isMember = User.IsInRole("Member") && !User.IsInRole("CenterManager") && !User.IsInRole("Receptionist");
        PagedResult<ClassViewModel> result = await _classService.GetClassesPagedAsync(
            page, pageSize, search, sportId, status, isMember);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetClassByIdAsync([FromRoute] int id)
    {
        bool isMember = User.IsInRole("Member") && !User.IsInRole("CenterManager") && !User.IsInRole("Receptionist");
        ClassViewModel? result = await _classService.GetClassByIdAsync(id, isMember);
        if (result is null)
        {
            return NotFound(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = "CLASS_NOT_FOUND",
                    Message = "Không tìm thấy lớp học."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CreateClassAsync([FromBody] CreateClassAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassViewModel result = await _classService.CreateClassAsync(actorAccountId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateClassAsync(
        [FromRoute] int id,
        [FromBody] UpdateClassAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassViewModel result = await _classService.UpdateClassAsync(actorAccountId, id, request);
        return Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateClassStatusAsync(
        [FromRoute] int id,
        [FromBody] UpdateClassStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        ClassViewModel result = await _classService.UpdateClassStatusAsync(actorAccountId, id, request.Status);
        return Ok(result);
    }

    [HttpGet("{id:int}/cancel-impact")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> GetClassCancelImpactAsync([FromRoute] int id)
    {
        ClassCancelImpactViewModel result = await _classService.GetClassCancelImpactAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:int}/cancel-future-sessions")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CancelFutureSessionsAsync(
        [FromRoute] int id,
        [FromBody] CancelFutureSessionsAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        CancelFutureSessionsResultViewModel result = await _classService.CancelFutureSessionsAsync(
            actorAccountId, id, request.Reason);
        return Ok(result);
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
