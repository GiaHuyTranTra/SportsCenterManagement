using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel.Member;
using APIViewModel.MemberSubscription;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MemberService;
using Services.MemberSubscriptionService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    [HttpPost]
    public async Task<IActionResult> CreateManagedMemberAsync(
        [FromBody] CreateManagedMemberAPIViewModel request)
    {
        (CreateManagedMemberResult Result, CreateManagedMemberResponseAPIViewModel? Data) result =
            await _memberService.CreateManagedMemberAsync(request);

        switch (result.Result)
        {
            case CreateManagedMemberResult.Success when result.Data is not null:
                return CreatedAtRoute(
                    "GetMemberById",
                    new { accountId = result.Data.Member.AccountId },
                    result.Data);
            case CreateManagedMemberResult.DuplicateEmail:
                return Conflict("Email already exists");
            case CreateManagedMemberResult.DuplicatePhone:
                return Conflict("Phone number already exists");
            case CreateManagedMemberResult.InvalidData:
                return BadRequest("Invalid member data");
            case CreateManagedMemberResult.MemberRoleMissing:
                return StatusCode(500);
            default:
                return StatusCode(500);
        }
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

    [Authorize(Roles = "CenterManager")]
    [HttpGet("{accountId}", Name = "GetMemberById")]
    public async Task<IActionResult> GetMemberByIdAsync([FromRoute] string accountId)
    {
        MemberDetailAPIViewModel? result = await _memberService.GetMemberByIdAsync(accountId);

        if (result is null)
        {
            return NotFound("Member not found");
        }

        return Ok(result);
    }

    [Authorize(Roles = "Receptionist,CenterManager")]
    [HttpGet("membership-status")]
    public async Task<IActionResult> GetMembershipStatusesAsync(
        [FromQuery] string? search = null,
        [FromQuery] string? filter = "ALL")
    {
        string normalizedFilter = string.IsNullOrWhiteSpace(filter)
            ? "ALL"
            : filter.Trim().ToUpperInvariant();
        if (normalizedFilter != "ALL" &&
            normalizedFilter != "ACTIVE" &&
            normalizedFilter != "EXPIRING" &&
            normalizedFilter != "EXPIRED" &&
            normalizedFilter != "SUSPENDED" &&
            normalizedFilter != "UPCOMING" &&
            normalizedFilter != "PENDING_PAYMENT" &&
            normalizedFilter != "NONE")
        {
            return BadRequest("Invalid membership status filter.");
        }

        List<MembershipStatusAPIViewModel> rows =
            await _memberService.GetMembershipStatusesAsync(search, normalizedFilter);
        return Ok(rows);
    }

    [Authorize(Roles = "CenterManager")]
    [HttpPatch("{accountId}")]
    public async Task<IActionResult> UpdateMemberAsync(
        [FromRoute] string accountId,
        [FromBody] UpdateManagedMemberAPIViewModel request)
    {
        (UpdateManagedMemberResult Result, MemberDetailAPIViewModel? Data) result =
            await _memberService.UpdateManagedMemberAsync(accountId, request);

        switch (result.Result)
        {
            case UpdateManagedMemberResult.Success when result.Data is not null:
                return Ok(result.Data);
            case UpdateManagedMemberResult.NotFound:
                return NotFound("Member not found");
            case UpdateManagedMemberResult.DuplicateEmail:
                return Conflict("Email already exists");
            case UpdateManagedMemberResult.DuplicatePhone:
                return Conflict("Phone number already exists");
            case UpdateManagedMemberResult.InvalidData:
                return BadRequest("Invalid input data");
            default:
                return StatusCode(500);
        }
    }

    [Authorize(Roles = "CenterManager")]
    [HttpDelete("{accountId}")]
    public async Task<IActionResult> SoftDeleteMemberAsync([FromRoute] string accountId)
    {
        DeleteMemberResult result = await _memberService.SoftDeleteMemberAsync(accountId);

        switch (result)
        {
            case DeleteMemberResult.Success:
                return NoContent();
            case DeleteMemberResult.NotFound:
                return NotFound("Member not found");
            case DeleteMemberResult.AlreadyDeleted:
                return BadRequest("Member is already deleted");
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
