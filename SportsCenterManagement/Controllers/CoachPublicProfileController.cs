using APIViewModel.Coach;
using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.CoachService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/coaches")]
[Authorize(Roles = "Member,Coach,Receptionist,CenterManager")]
public sealed class CoachPublicProfileController : ControllerBase
{
    private readonly ICoachService _coachService;

    public CoachPublicProfileController(ICoachService coachService)
    {
        _coachService = coachService;
    }

    [HttpGet("{accountId}/public-profile")]
    public async Task<ActionResult<CoachPublicProfileAPIViewModel>> GetAsync(string accountId)
    {
        CoachPublicProfileAPIViewModel? profile = await _coachService.GetPublicProfileAsync(accountId);
        if (profile is null)
        {
            return NotFound(new ApiErrorResponseAPIViewModel
            {
                Error = new ApiErrorAPIViewModel
                {
                    Code = "COACH_PUBLIC_PROFILE_NOT_FOUND",
                    Message = "Không tìm thấy hồ sơ công khai của huấn luyện viên."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(profile);
    }
}
