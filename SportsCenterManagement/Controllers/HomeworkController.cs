using System.Security.Claims;
using APIViewModel.Common;
using APIViewModel.Homework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.HomeworkAssignmentService;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class HomeworkController : ControllerBase
{
    private readonly IHomeworkAssignmentService _service;

    public HomeworkController(IHomeworkAssignmentService service)
    {
        _service = service;
    }

    private string CallerId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpPost]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<IReadOnlyList<HomeworkAssignmentAPIViewModel>>> AssignAsync(
        [FromBody] CreateHomeworkAssignmentAPIViewModel request)
    {
        var (result, items) = await _service.AssignAsync(CallerId, request);
        return result switch
        {
            AssignHomeworkResult.Success => Ok(items),
            AssignHomeworkResult.InvalidRequest => Error(
                StatusCodes.Status400BadRequest,
                "INVALID_HOMEWORK_TARGET",
                "Đối tượng, tiêu đề hoặc nội dung bài tập không hợp lệ."),
            AssignHomeworkResult.TargetNotFoundOrForbidden => Error(
                StatusCodes.Status404NotFound,
                "HOMEWORK_TARGET_NOT_FOUND",
                "Không tìm thấy đối tượng bài tập trong phạm vi giảng dạy của bạn."),
            AssignHomeworkResult.TargetHasNoMembers => Error(
                StatusCodes.Status409Conflict,
                "HOMEWORK_TARGET_HAS_NO_MEMBERS",
                "Đối tượng hiện không có học viên hợp lệ để nhận bài tập."),
            _ => Error(StatusCodes.Status500InternalServerError, "HOMEWORK_ASSIGN_FAILED", "Không thể giao bài tập.")
        };
    }

    [HttpGet("coach/me")]
    [Authorize(Roles = "Coach")]
    public async Task<ActionResult<IReadOnlyList<HomeworkAssignmentAPIViewModel>>> GetCoachHistoryAsync() =>
        Ok(await _service.GetCoachHistoryAsync(CallerId));

    [HttpGet("member/me")]
    [Authorize(Roles = "Member")]
    public async Task<ActionResult<IReadOnlyList<HomeworkAssignmentAPIViewModel>>> GetMemberHistoryAsync() =>
        Ok(await _service.GetMemberHistoryAsync(CallerId));

    private ObjectResult Error(int statusCode, string code, string message) =>
        StatusCode(statusCode, new ApiErrorResponseAPIViewModel
        {
            Error = new ApiErrorAPIViewModel { Code = code, Message = message },
            TraceId = HttpContext.TraceIdentifier
        });
}
