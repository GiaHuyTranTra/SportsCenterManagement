using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.Member;
using APIViewModel.MemberSubscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MemberService;
using Services.MemberSubscriptionService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[TypeFilter(typeof(AuthFilter))]
public class MemberController : ControllerBase
{
    private readonly IMemberService _memberService;
    private readonly IMemberSubscriptionService _memberSubscriptionService;

    public MemberController(
        IMemberService memberService,
        IMemberSubscriptionService memberSubscriptionService)
    {
        _memberService = memberService;
        _memberSubscriptionService = memberSubscriptionService;
    }

    [Authorize(Roles = "Receptionist,CenterManager")]
    [HttpPost("counter-registration")]
    public async Task<IActionResult> RegisterMemberAtCounterAsync(
        [FromBody] CounterRegisterMemberAPIViewModel request)
    {
        string? staffAccountId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(staffAccountId))
        {
            return Unauthorized();
        }

        (CounterRegisterMemberResult Result, CounterRegisterMemberResponseAPIViewModel? Data) result =
            await _memberSubscriptionService.RegisterMemberAtCounterAsync(
                staffAccountId,
                request);

        switch (result.Result)
        {
            case CounterRegisterMemberResult.Success when result.Data is not null:
                return StatusCode(StatusCodes.Status201Created, result.Data);
            case CounterRegisterMemberResult.InvalidData:
            case CounterRegisterMemberResult.InvalidPaymentMethod:
                return BadRequest("Invalid counter registration data.");
            case CounterRegisterMemberResult.StaffNotFound:
            case CounterRegisterMemberResult.PackageNotFound:
                return NotFound();
            case CounterRegisterMemberResult.StaffRoleNotAllowed:
            case CounterRegisterMemberResult.StaffInactive:
                return StatusCode(StatusCodes.Status403Forbidden);
            case CounterRegisterMemberResult.StaffLocked:
                return StatusCode(StatusCodes.Status423Locked);
            case CounterRegisterMemberResult.DuplicateEmail:
            case CounterRegisterMemberResult.DuplicatePhone:
            case CounterRegisterMemberResult.PackageInactive:
            case CounterRegisterMemberResult.PendingOrderExists:
            case CounterRegisterMemberResult.PriceChanged:
            case CounterRegisterMemberResult.InvoiceNumberCollision:
            case CounterRegisterMemberResult.ConcurrencyConflict:
                return Conflict();
            case CounterRegisterMemberResult.MemberRoleMissing:
                return StatusCode(StatusCodes.Status500InternalServerError);
            default:
                return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [Authorize(Roles = "CenterManager,Receptionist")]
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

    [Authorize(Roles = "CenterManager,Receptionist")]
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

    [Authorize(Roles = "Receptionist")]
    [HttpGet("quick-search")]
    public async Task<IActionResult> QuickSearchAsync([FromQuery] string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return BadRequest("Keyword is required");
        }

        List<MemberSearchAPIViewModel> results = await _memberService.QuickSearchMembersAsync(keyword);
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
