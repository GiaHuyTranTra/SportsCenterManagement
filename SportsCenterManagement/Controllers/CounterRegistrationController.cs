using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using SportsCenterManagement.DTOs.CounterRegistration;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Receptionist,CenterManager")]
public class CounterRegistrationController : ControllerBase
{
	private readonly ICounterRegistrationService _registrationService;

	public CounterRegistrationController(ICounterRegistrationService registrationService) => _registrationService = registrationService;

	[HttpPost("register-member")]
	public async Task<IActionResult> RegisterMember([FromBody] CounterRegisterMemberDto dto)
	{
		if (!ModelState.IsValid) return BadRequest(ModelState);
		var accountId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
		var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
		try
		{
			var result = await _registrationService.RegisterMemberAtCounterAsync(dto, accountId, ipAddress);
			return Ok(new { success = true, message = "Đăng ký thành viên tại quầy thành công", data = result });
		}
		catch (InvalidOperationException ex) { return Conflict(new { success = false, message = ex.Message }); }
		catch (KeyNotFoundException ex) { return NotFound(new { success = false, message = ex.Message }); }
	}
}
