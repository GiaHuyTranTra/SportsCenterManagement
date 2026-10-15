using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.ClassRegistration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.ClassRegistrationService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class ClassRegistrationController : ControllerBase
{
    private readonly IClassRegistrationService _registrationService;

    public ClassRegistrationController(IClassRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> GetMyRegistrationsAsync([FromQuery] bool includeCanceled = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
    {
        string? memberAccountId = GetActorAccountId();
        if (memberAccountId is null)
        {
            return Unauthorized();
        }

        PagedResult<ClassRegistrationViewModel> result = await _registrationService.GetMyRegistrationsAsync(
            memberAccountId, includeCanceled, page, pageSize, from, to);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<IActionResult> GetRegistrationsAsync(
        [FromQuery] string? memberAccountId = null,
        [FromQuery] bool includeCanceled = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        PagedResult<ClassRegistrationViewModel> result = await _registrationService.GetRegistrationsForStaffAsync(
            memberAccountId, includeCanceled, page, pageSize);
        return Ok(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterMemberSessionAPIViewModel request)
    {
        string? memberAccountId = GetActorAccountId();
        if (memberAccountId is null)
        {
            return Unauthorized();
        }

        ClassRegistrationViewModel result = await _registrationService.RegisterAsync(
            memberAccountId, request.SessionId, "MEMBER", memberAccountId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("counter-register")]
    [Authorize(Roles = "Receptionist")]
    public async Task<IActionResult> CounterRegisterAsync([FromBody] CounterRegisterSessionAPIViewModel request)
    {
        string? staffAccountId = GetActorAccountId();
        if (staffAccountId is null)
        {
            return Unauthorized();
        }

        ClassRegistrationViewModel result = await _registrationService.RegisterAsync(
            request.MemberAccountId, request.SessionId, "RECEPTIONIST", staffAccountId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{id:int}/cancel")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> CancelRegistrationAsync(
        [FromRoute] int id,
        [FromBody] CancelRegistrationAPIViewModel request)
    {
        string? memberAccountId = GetActorAccountId();
        if (memberAccountId is null)
        {
            return Unauthorized();
        }

        ClassRegistrationViewModel result = await _registrationService.CancelRegistrationAsync(
            id, request.Reason, memberAccountId, false);
        return Ok(result);
    }

    [HttpPost("{id:int}/counter-cancel")]
    [Authorize(Roles = "Receptionist")]
    public async Task<IActionResult> CounterCancelRegistrationAsync(
        [FromRoute] int id,
        [FromBody] CounterCancelRegistrationAPIViewModel request)
    {
        string? staffAccountId = GetActorAccountId();
        if (staffAccountId is null)
        {
            return Unauthorized();
        }

        ClassRegistrationViewModel result = await _registrationService.CancelRegistrationAsync(
            id, request.Reason, staffAccountId, true);
        return Ok(result);
    }

    private string? GetActorAccountId()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(accountId) ? null : accountId;
    }
}
