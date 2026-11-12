using System.Security.Claims;
using APIViewModel.Common;
using APIViewModel.MembershipTerms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.MembershipTermsService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class MembershipTermsController : ControllerBase
{
    private readonly IMembershipTermsService _termsService;

    public MembershipTermsController(IMembershipTermsService termsService)
    {
        _termsService = termsService;
    }

    [HttpGet("current")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<CurrentMembershipTermsAPIViewModel>> GetCurrentAsync()
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return Unauthorized();
        }

        return Ok(await _termsService.GetCurrentAsync(memberId));
    }

    [HttpPost("current/accept")]
    [Authorize(Roles = "Member")]
    public async Task<IActionResult> AcceptCurrentAsync([FromBody] AcceptMembershipTermsAPIViewModel request)
    {
        string? memberId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(memberId))
        {
            return Unauthorized();
        }

        AcceptMembershipTermsResult result = await _termsService.AcceptCurrentAsync(
            memberId,
            request.Version,
            request.ContentHash);
        return result switch
        {
            AcceptMembershipTermsResult.Success => Ok(await _termsService.GetMemberStatusAsync(memberId)),
            AcceptMembershipTermsResult.VersionMismatch => Error(
                StatusCodes.Status409Conflict,
                "TERMS_VERSION_MISMATCH",
                "Điều khoản đã thay đổi. Vui lòng tải lại và đọc phiên bản mới nhất."),
            AcceptMembershipTermsResult.MemberNotEligible => Error(
                StatusCodes.Status403Forbidden,
                "MEMBER_NOT_ELIGIBLE",
                "Tài khoản học viên không đủ điều kiện chấp nhận điều khoản."),
            _ => Error(StatusCodes.Status404NotFound, "MEMBER_NOT_FOUND", "Không tìm thấy học viên.")
        };
    }

    [HttpGet("members/{memberId}/status")]
    [Authorize(Roles = "Receptionist,CenterManager")]
    public async Task<ActionResult<MemberTermsStatusAPIViewModel>> GetMemberStatusAsync(string memberId)
    {
        return Ok(await _termsService.GetMemberStatusAsync(memberId));
    }

    private ObjectResult Error(int statusCode, string code, string message)
    {
        return StatusCode(statusCode, new ApiErrorResponseAPIViewModel
        {
            Error = new ApiErrorAPIViewModel { Code = code, Message = message },
            TraceId = HttpContext.TraceIdentifier
        });
    }
}
