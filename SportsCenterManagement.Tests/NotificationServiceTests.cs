using System;
using System.Threading.Tasks;
using APIViewModel;
using APIViewModel.Notification;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.NotificationService;
using Services.ScheduleQueryService;
using Xunit;

namespace SportsCenterManagement.Tests;

public class NotificationServiceTests
{
    [Fact]
    public async Task GetMyNotificationsAsync_FiltersByRecipientAndReadStatus()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Notifications.AddRange(
            new Notification
            {
                Id = 1,
                RecipientId = "member-1",
                Type = "CLASS_REGISTERED",
                Title = "Registered",
                Body = "Body 1",
                EntityType = "ClassSession",
                EntityId = "1",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                Id = 2,
                RecipientId = "member-1",
                Type = "SESSION_RESCHEDULED",
                Title = "Rescheduled",
                Body = "Body 2",
                EntityType = "ClassSession",
                EntityId = "2",
                IsRead = true,
                ReadAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            },
            new Notification
            {
                Id = 3,
                RecipientId = "other-member",
                Type = "SESSION_CANCELED",
                Title = "Canceled",
                Body = "Body 3",
                EntityType = "ClassSession",
                EntityId = "3",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        NotificationService service = new NotificationService(context);

        PagedResult<NotificationViewModel> allResult = await service.GetMyNotificationsAsync("member-1", 1, 10, null);
        Assert.Equal(2, allResult.TotalItems);

        PagedResult<NotificationViewModel> unreadResult = await service.GetMyNotificationsAsync("member-1", 1, 10, false);
        Assert.Equal(1, unreadResult.TotalItems);
        Assert.Equal("Registered", unreadResult.Items[0].Title);
    }

    [Fact]
    public async Task MarkAsReadAsync_ValidRecipient_UpdatesStatus()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Notifications.Add(new Notification
        {
            Id = 10,
            RecipientId = "member-1",
            Type = "CLASS_REGISTERED",
            Title = "Registered",
            Body = "Details",
            EntityType = "ClassSession",
            EntityId = "1",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        NotificationService service = new NotificationService(context);
        NotificationViewModel result = await service.MarkAsReadAsync("member-1", 10, true);

        Assert.True(result.IsRead);

        Notification updated = await context.Notifications.FirstAsync(n => n.Id == 10);
        Assert.True(updated.IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_OtherRecipient_ThrowsForbidden()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Notifications.Add(new Notification
        {
            Id = 11,
            RecipientId = "member-1",
            Type = "CLASS_REGISTERED",
            Title = "Registered",
            Body = "Details",
            EntityType = "ClassSession",
            EntityId = "1",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        NotificationService service = new NotificationService(context);
        BusinessRuleException exception = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.MarkAsReadAsync("other-user", 11, true));

        Assert.Equal("FORBIDDEN", exception.Code);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }
}
