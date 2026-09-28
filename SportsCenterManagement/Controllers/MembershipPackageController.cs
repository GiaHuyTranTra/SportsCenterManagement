using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MembershipPackageController : ControllerBase
{
	private readonly IMembershipPackageService _packageService;

	public MembershipPackageController(IMembershipPackageService packageService) => _packageService = packageService;

	[HttpGet("active")]
	public async Task<IActionResult> GetActivePackages()
	{
		var packages = await _packageService.GetActivePackagesAsync();
		return Ok(new { success = true, data = packages });
	}
}
