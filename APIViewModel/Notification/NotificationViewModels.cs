using System;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Notification;

public class NotificationViewModel
{
    public int Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class UpdateNotificationReadAPIViewModel
{
    [Required(ErrorMessage = "Trạng thái đã đọc không được để trống.")]
    public bool? IsRead { get; set; }
}
