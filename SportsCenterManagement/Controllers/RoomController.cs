using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Room;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.RoomService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class RoomController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomController(IRoomService roomService)
    {
        _roomService = roomService;
    }

    [HttpGet]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetRoomsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        PagedResult<RoomViewModel> result = await _roomService.GetRoomsPagedAsync(
            page, pageSize, search, isActive);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "CenterManager,Coach,Member,Receptionist")]
    public async Task<IActionResult> GetRoomByIdAsync([FromRoute] int id)
    {
        RoomViewModel? room = await _roomService.GetRoomByIdAsync(id);
        if (room is null)
        {
            return NotFound(new APIViewModel.Common.ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new APIViewModel.Common.ApiErrorAPIViewModel
                {
                    Code = "ROOM_NOT_FOUND",
                    Message = "Không tìm thấy phòng tập."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(room);
    }

    [HttpPost]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> CreateRoomAsync([FromBody] CreateRoomAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        RoomViewModel result = await _roomService.CreateRoomAsync(actorAccountId, request);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateRoomAsync(
        [FromRoute] int id,
        [FromBody] UpdateRoomAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        RoomViewModel result = await _roomService.UpdateRoomAsync(actorAccountId, id, request);
        return Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "CenterManager")]
    public async Task<IActionResult> UpdateRoomStatusAsync(
        [FromRoute] int id,
        [FromBody] UpdateRoomStatusAPIViewModel request)
    {
        string? actorAccountId = GetActorAccountId();
        if (actorAccountId is null)
        {
            return Unauthorized();
        }

        if (!request.IsActive.HasValue)
        {
            return BadRequest(new APIViewModel.Common.ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new APIViewModel.Common.ApiErrorAPIViewModel
                {
                    Code = "VALIDATION_ERROR",
                    Message = "Trạng thái không được để trống."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        RoomViewModel result = await _roomService.UpdateRoomStatusAsync(actorAccountId, id, request.IsActive.Value);
        return Ok(result);
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
