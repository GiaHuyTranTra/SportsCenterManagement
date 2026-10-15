using Services.ClassScheduling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Notification;
using DataAccess.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Services.ScheduleQueryService;

namespace Services.NotificationService;

public class NotificationService : INotificationService
{
    private readonly SportsCenterManagementContext _context;

    public NotificationService(SportsCenterManagementContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<NotificationViewModel>> GetMyNotificationsAsync(
        string recipientId,
        int page,
        int pageSize,
        bool? isRead)
    {
        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Notification> query = _context.Notifications.AsNoTracking()
            .Where(item => item.RecipientId == recipientId);

        if (isRead.HasValue)
        {
            query = query.Where(item => item.ReadAt.HasValue == isRead.Value);
        }

        int totalItems = await query.CountAsync();

        List<NotificationViewModel> items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(item => new NotificationViewModel
            {
                Id = item.Id,
                Type = item.Type,
                Title = item.Title,
                Body = item.Body,
                EntityType = item.EntityType,
                EntityId = item.EntityId,
                IsRead = item.ReadAt.HasValue,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync();

        return new PagedResult<NotificationViewModel>
        {
            Items = items,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalItems = totalItems
        };
    }

    public async Task<NotificationViewModel> MarkAsReadAsync(
        string recipientId,
        int notificationId,
        bool isRead)
    {
        Notification? notification = await _context.Notifications
            .FirstOrDefaultAsync(item => item.Id == notificationId);

        if (notification is null)
        {
            throw new BusinessRuleException("NOTIFICATION_NOT_FOUND", "Không tìm thấy thông báo.");
        }

        if (notification.RecipientId != recipientId)
        {
            throw new BusinessRuleException("FORBIDDEN", "Bạn không có quyền thao tác trên thông báo này.");
        }

        notification.IsRead = isRead;
        notification.ReadAt = isRead ? notification.ReadAt ?? DateTime.UtcNow : null;
        await _context.SaveChangesAsync();

        return new NotificationViewModel
        {
            Id = notification.Id,
            Type = notification.Type,
            Title = notification.Title,
            Body = notification.Body,
            EntityType = notification.EntityType,
            EntityId = notification.EntityId,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        };
    }

    public async Task SendNotificationAsync(
        string recipientId,
        string type,
        string title,
        string body,
        string entityType,
        string entityId,
        DateTime eventAt)
    {
        Flow2WriteScope operation = Flow2WriteScope.Current(_context)
            ?? throw new InvalidOperationException("Notifications must be staged by a Flow 2 command.");
        if (entityType != "ClassSession" || !int.TryParse(entityId, out int sessionId))
            throw new ArgumentException("A notification must identify a session.");
        ClassChangeEvent? change = _context.ClassChangeEvents.Local.FirstOrDefault(item =>
            item.OperationId == operation.OperationId && item.SessionId == sessionId && item.EventType == type);
        change ??= await _context.ClassChangeEvents.FirstOrDefaultAsync(item =>
            item.OperationId == operation.OperationId && item.SessionId == sessionId && item.EventType == type);
        if (change is null)
        {
            change = new ClassChangeEvent
            {
                Id = Guid.NewGuid(), OperationId = operation.OperationId, SessionId = sessionId,
                EventType = type, ActorAccountId = operation.ActorAccountId, OccurredAt = DateTime.UtcNow
            };
            _context.ClassChangeEvents.Add(change);
        }
        Guid eventId = change.Id;
        bool exists = _context.Notifications.Local.Any(item => item.EventId == eventId && item.RecipientId == recipientId)
            || await _context.Notifications.AnyAsync(item => item.EventId == eventId && item.RecipientId == recipientId);
        if (exists) return;
        _context.Notifications.Add(new Notification
        {
            EventId = eventId, RecipientId = recipientId, Type = type, Title = title, Body = body,
            EntityType = entityType, EntityId = entityId, EventAt = change.OccurredAt,
            CreatedAt = change.OccurredAt, IsRead = false
        });
    }
}
