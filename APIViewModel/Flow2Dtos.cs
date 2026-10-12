using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel;

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public class RoomItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateRoomRequestDto
{
    [Required(ErrorMessage = "Tên phòng bắt buộc")]
    [MaxLength(80, ErrorMessage = "Tên phòng tối đa 80 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(120, ErrorMessage = "Vị trí tối đa 120 ký tự")]
    public string? Location { get; set; }

    [Range(1, 500, ErrorMessage = "Sức chứa phải là số nguyên dương")]
    public int Capacity { get; set; }
}

public class UpdateRoomRequestDto : CreateRoomRequestDto
{
}

public class UpdateRoomStatusDto
{
    public bool IsActive { get; set; }
}

public class AssignCoachRequestDto
{
    [Required(ErrorMessage = "CoachId không được để trống")]
    public string CoachId { get; set; } = string.Empty;
}

public class SessionResponseDto
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string CoachId { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string StartsAt { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int BookedCount { get; set; }
    public int RemainingCapacity => Math.Max(0, Capacity - BookedCount);
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}