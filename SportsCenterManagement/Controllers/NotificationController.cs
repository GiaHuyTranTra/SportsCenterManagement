using System.Security.Claims;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Services.NotificationService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[TypeFilter(typeof(AuthFilter))]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("me")]
    [Authorize(Roles = "Member,Coach")]
    public async Task<IActionResult> GetMyNotificationsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null)
    {
        string? recipientId = GetActorAccountId();
        if (recipientId is null)
        {
            return Unauthorized();
        }

        PagedResult<NotificationViewModel> result = await _notificationService.GetMyNotificationsAsync(
            recipientId,
            page,
            pageSize,
            isRead);

        return Ok(result);
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsReadAsync(
        [FromRoute] int id,
        [FromBody] UpdateNotificationReadAPIViewModel request)
    {
        string? recipientId = GetActorAccountId();
        if (recipientId is null)
        {
            return Unauthorized();
        }

        NotificationViewModel result = await _notificationService.MarkAsReadAsync(
            recipientId,
            id,
            request.IsRead!.Value);

        return Ok(result);
    }

    private string? GetActorAccountId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
