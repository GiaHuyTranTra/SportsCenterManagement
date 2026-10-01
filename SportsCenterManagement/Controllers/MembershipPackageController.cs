using APIViewModel.MembershipPackage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipPackageService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "CenterManager")]
public class MembershipPackageController : ControllerBase
{
    private readonly IMembershipPackageService _membershipPackageService;

    public MembershipPackageController(IMembershipPackageService membershipPackageService)
    {
        _membershipPackageService = membershipPackageService;
    }

    [AllowAnonymous]
    [HttpGet("active")]
    public async Task<IActionResult> GetActivePackagesAsync()
    {
        List<ActiveMembershipPackageAPIViewModel> result =
            await _membershipPackageService.GetActivePackagesAsync();

        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetPackagesAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        if (!IsValidStatus(status))
        {
            return BadRequest("Invalid status filter. Allowed values: Active, Inactive.");
        }

        PagedMembershipPackageResultAPIViewModel result =
            await _membershipPackageService.GetPackagesAsync(page, pageSize, search, status);

        return Ok(result);
    }

    [HttpGet("{id:int}", Name = "GetMembershipPackageById")]
    public async Task<IActionResult> GetPackageByIdAsync([FromRoute] int id)
    {
        MembershipPackageDetailAPIViewModel? package =
            await _membershipPackageService.GetPackageByIdAsync(id);

        if (package is null)
        {
            return NotFound("Membership package not found");
        }

        return Ok(package);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePackageAsync(
        [FromBody] MembershipPackageInputAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        (CreatePackageResult Result, MembershipPackageDetailAPIViewModel? Package) result =
            await _membershipPackageService.CreatePackageAsync(request);

        switch (result.Result)
        {
            case CreatePackageResult.Success when result.Package is not null:
                return CreatedAtRoute(
                    "GetMembershipPackageById",
                    new { id = result.Package.Id },
                    result.Package);
            case CreatePackageResult.DuplicateName:
                return Conflict("Membership package name already exists");
            case CreatePackageResult.InvalidData:
                return BadRequest("Invalid membership package data");
            default:
                return StatusCode(500);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePackageAsync(
        [FromRoute] int id,
        [FromBody] MembershipPackageInputAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        (UpdatePackageResult Result, MembershipPackageDetailAPIViewModel? Package) result =
            await _membershipPackageService.UpdatePackageAsync(id, request);

        switch (result.Result)
        {
            case UpdatePackageResult.Success when result.Package is not null:
                return Ok(result.Package);
            case UpdatePackageResult.NotFound:
                return NotFound("Membership package not found");
            case UpdatePackageResult.DuplicateName:
                return Conflict("Membership package name already exists");
            case UpdatePackageResult.InvalidData:
                return BadRequest("Invalid membership package data");
            default:
                return StatusCode(500);
        }
    }

    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdatePackageStatusAsync(
        [FromRoute] int id,
        [FromBody] UpdateMembershipPackageStatusAPIViewModel request)
    {
        if (!ModelState.IsValid || !request.IsActive.HasValue)
        {
            return BadRequest(ModelState);
        }

        UpdatePackageStatusResult result = await _membershipPackageService
            .UpdatePackageStatusAsync(id, request.IsActive.Value);

        switch (result)
        {
            case UpdatePackageStatusResult.Success:
                return Ok("Update membership package status successful");
            case UpdatePackageStatusResult.NotFound:
                return NotFound("Membership package not found");
            default:
                return StatusCode(500);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePackageAsync([FromRoute] int id)
    {
        DeletePackageResult result = await _membershipPackageService.DeletePackageAsync(id);

        switch (result)
        {
            case DeletePackageResult.Success:
                return NoContent();
            case DeletePackageResult.NotFound:
                return NotFound("Membership package not found");
            case DeletePackageResult.PackageInUse:
                return Conflict("Cannot delete membership package because it has existing subscriptions.");
            default:
                return StatusCode(500);
        }
    }

    private static bool IsValidStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        string normalizedStatus = status.Trim();
        return string.Equals(normalizedStatus, "Active", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedStatus, "Inactive", StringComparison.OrdinalIgnoreCase);
    }
}
