using System.Security.Claims;
using APIViewModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Flow2PhucService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClassSessionController : ControllerBase
{
    private readonly IFlow2PhucService _service;

    public ClassSessionController(IFlow2PhucService service)
    {
        _service = service;
    }

    [HttpPut("{id:int}/coach")]
    [Authorize(Roles = "CenterManager,CENTER_MANAGER")]
    public async Task<IActionResult> AssignCoach(int id, [FromBody] AssignCoachRequestDto request)
    {
        try
        {
            string actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
            SessionResponseDto result = await _service.AssignCoachAsync(id, request.CoachId, actorId);
            return Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException exception)
        {
            return BadRequest(new
            {
                success = false,
                error = new { code = exception.Code, message = exception.Message },
                traceId = HttpContext.TraceIdentifier
            });
        }
    }
}