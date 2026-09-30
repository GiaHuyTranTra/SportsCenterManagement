using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.Member;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MemberService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class MemberController : ControllerBase
{
    private readonly IMemberService _memberService;

    public MemberController(IMemberService memberService)
    {
        _memberService = memberService;
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet]
    public async Task<IActionResult> GetMembersAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            string trimmedStatus = status.Trim();
            if (!string.Equals(trimmedStatus, "Active", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(trimmedStatus, "Inactive", System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid status filter. Allowed values: Active, Inactive.");
            }
        }

        PagedMemberResultAPIViewModel result = await _memberService.GetMembersAsync(page, pageSize, search, status);
        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpGet("{accountId}")]
    public async Task<IActionResult> GetMemberByIdAsync([FromRoute] string accountId)
    {
        MemberDetailAPIViewModel? result = await _memberService.GetMemberByIdAsync(accountId);

        if (result is null)
        {
            return NotFound("Member not found");
        }

        return Ok(result);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateMemberAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateMemberAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UpdateMemberResult result = await _memberService.UpdateMemberAsync(accountId, request);

        switch (result)
        {
            case UpdateMemberResult.Success:
                return Ok("Update member successful");
            case UpdateMemberResult.NotFound:
                return NotFound("Member not found");
            case UpdateMemberResult.DuplicatePhone:
                return Conflict("Phone number already exists");
            case UpdateMemberResult.InvalidData:
                return BadRequest("Invalid input data");
            case UpdateMemberResult.NoChanges:
                return BadRequest("No changes provided");
            default:
                return StatusCode(500);
        }
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}/status")]
    public async Task<IActionResult> UpdateMemberStatusAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateMemberStatusAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UpdateMemberStatusResult result = await _memberService.UpdateMemberStatusAsync(accountId, request.Status);

        switch (result)
        {
            case UpdateMemberStatusResult.Success:
                return Ok("Update member status successful");
            case UpdateMemberStatusResult.NotFound:
                return NotFound("Member not found");
            case UpdateMemberStatusResult.InvalidStatus:
                return BadRequest("Invalid status. Allowed values: Active, Inactive.");
            default:
                return StatusCode(500);
        }
    }

    [Authorize(Roles = "Receptionist,CenterManager")]
    [HttpGet("quick-search")]
    public async Task<IActionResult> QuickSearchAsync(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return BadRequest("Keyword is required");
        }

        PagedMemberSearchResultAPIViewModel results =
            await _memberService.QuickSearchMembersAsync(keyword, page, pageSize);
        return Ok(results);
    }

    [Authorize(Roles = "Member")]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfileAsync()
    {
        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized();
        }

        MemberDetailAPIViewModel? result = await _memberService.GetMemberByIdAsync(accountId);

        if (result is null)
        {
            return NotFound("Member not found");
        }

        return Ok(result);
    }

    [Authorize(Roles = "Member")]
    [HttpPatch("profile")]
    public async Task<IActionResult> UpdateProfileAsync([FromBody] UpdateMemberAPIViewModel request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string? accountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Unauthorized();
        }

        UpdateMemberResult result = await _memberService.UpdateMemberAsync(accountId, request);

        switch (result)
        {
            case UpdateMemberResult.Success:
                return Ok("Update profile successful");
            case UpdateMemberResult.NotFound:
                return NotFound("Member not found");
            case UpdateMemberResult.DuplicatePhone:
                return Conflict("Phone number already exists");
            case UpdateMemberResult.InvalidData:
                return BadRequest("Invalid input data");
            case UpdateMemberResult.NoChanges:
                return BadRequest("No changes provided");
            default:
                return StatusCode(500);
        }
    }
}
