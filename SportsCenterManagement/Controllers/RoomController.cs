using APIViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Flow2PhucService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoomController : ControllerBase
{
    private readonly IFlow2PhucService _service;

    public RoomController(IFlow2PhucService service)
    {
        _service = service;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetRooms(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null)
    {
        PagedResult<RoomItemDto> result = await _service.GetRoomsAsync(page, pageSize, search, isActive);
        return Ok(new { success = true, data = result });
    }

    [HttpPost]
    [Authorize(Roles = "CenterManager,CENTER_MANAGER")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequestDto request)
    {
        try
        {
            RoomItemDto result = await _service.CreateRoomAsync(request);
            return StatusCode(StatusCodes.Status201Created, new { success = true, data = result });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(CreateError(exception));
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "CenterManager,CENTER_MANAGER")]
    public async Task<IActionResult> UpdateRoom(int id, [FromBody] UpdateRoomRequestDto request)
    {
        try
        {
            RoomItemDto result = await _service.UpdateRoomAsync(id, request);
            return Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(CreateError(exception));
        }
    }

    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = "CenterManager,CENTER_MANAGER")]
    public async Task<IActionResult> UpdateRoomStatus(int id, [FromBody] UpdateRoomStatusDto request)
    {
        try
        {
            RoomItemDto result = await _service.UpdateRoomStatusAsync(id, request.IsActive);
            return Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(CreateError(exception));
        }
    }

    private object CreateError(BusinessRuleException exception)
    {
        return new
        {
            success = false,
            error = new { code = exception.Code, message = exception.Message },
            traceId = HttpContext.TraceIdentifier
        };
    }
}