using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using APIViewModel.Class;

namespace APIViewModel.ClassRegistration;

/// <summary>
/// Một lượt đăng ký lớp học: thành viên đăng ký cả lớp, hệ thống giữ chỗ cho
/// mọi buổi còn ở tương lai của lớp. <c>Sessions</c> là các buổi đã giữ chỗ,
/// giữ lại để điểm danh và để hiển thị lịch cá nhân.
/// </summary>
public class ClassEnrollmentViewModel
{
    public int ClassId { get; set; }

    public string ClassName { get; set; } = string.Empty;

    public int SportId { get; set; }

    public string SportName { get; set; } = string.Empty;

    public string MemberAccountId { get; set; } = string.Empty;

    public string MemberName { get; set; } = string.Empty;

    public string MemberCode { get; set; } = string.Empty;

    /// <summary>BOOKED khi còn ít nhất một buổi đang giữ chỗ, ngược lại CANCELED.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    public string RegisteredVia { get; set; } = string.Empty;

    public string? RegisteredByName { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CanceledByName { get; set; }

    public string? CancelReason { get; set; }

    public int BookedSessionCount { get; set; }

    public int UpcomingSessionCount { get; set; }

    public List<ClassScheduleBlockViewModel> ScheduleBlocks { get; set; } = new List<ClassScheduleBlockViewModel>();

    public List<ClassRegistrationViewModel> Sessions { get; set; } = new List<ClassRegistrationViewModel>();
}

public class RegisterMemberClassAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
    public int ClassId { get; set; }
}

public class CounterRegisterClassAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Thành viên không được để trống.")]
    public string MemberAccountId { get; set; } = string.Empty;
}

public class ClassRegistrationViewModel
{
    public int Id { get; set; }

    public int SessionId { get; set; }

    public string MemberAccountId { get; set; } = string.Empty;

    public string MemberName { get; set; } = string.Empty;

    public string MemberCode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; }

    public string RegisteredVia { get; set; } = string.Empty;

    public string? RegisteredByName { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CanceledByName { get; set; }

    public string? CancelReason { get; set; }

    public ClassSessionViewDto Session { get; set; } = new ClassSessionViewDto();
}

public class RegisterMemberSessionAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Buổi học không hợp lệ.")]
    public int SessionId { get; set; }
}

public class CounterRegisterSessionAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Buổi học không hợp lệ.")]
    public int SessionId { get; set; }

    [Required(ErrorMessage = "Thành viên không được để trống.")]
    public string MemberAccountId { get; set; } = string.Empty;
}

public class CancelRegistrationAPIViewModel
{
    [StringLength(500, ErrorMessage = "Lý do hủy tối đa 500 ký tự.")]
    public string? Reason { get; set; }
}

public class CounterCancelRegistrationAPIViewModel
{
    [Required(ErrorMessage = "Lý do hủy không được để trống.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do hủy từ 3 đến 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}
