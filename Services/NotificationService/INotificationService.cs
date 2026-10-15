using System;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Notification;

namespace Services.NotificationService;

public interface INotificationService
{
    Task<PagedResult<NotificationViewModel>> GetMyNotificationsAsync(
        string recipientId,
        int page,
        int pageSize,
        bool? isRead);

    Task<NotificationViewModel> MarkAsReadAsync(
        string recipientId,
        int notificationId,
        bool isRead);

    Task SendNotificationAsync(
        string recipientId,
        string type,
        string title,
        string body,
        string entityType,
        string entityId,
        DateTime eventAt);
}
