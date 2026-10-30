using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Class;

/// <summary>
/// Lịch học của lớp: giáo viên đứng lớp, phòng, khoảng ngày và khung giờ cho
/// các buổi lặp lại theo thứ trong tuần. Tạo lớp phải kèm khối này để lớp có
/// buổi học ngay, không còn là lớp rỗng rồi thêm buổi từng ngày một.
/// </summary>
public class ClassScheduleAPIViewModel
{
    [Required(ErrorMessage = "Giáo viên đứng lớp không được để trống.")]
    public string CoachId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Phòng tập không hợp lệ.")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Ngày bắt đầu không được để trống.")]
    public string StartDate { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày kết thúc không được để trống.")]
    public string EndDate { get; set; } = string.Empty;

    /// <summary>Thứ trong tuần, 0 là Chủ nhật đến 6 là Thứ bảy.</summary>
    [Required(ErrorMessage = "Chọn ít nhất một thứ trong tuần.")]
    [MinLength(1, ErrorMessage = "Chọn ít nhất một thứ trong tuần.")]
    public List<int> DaysOfWeek { get; set; } = new List<int>();

    [Required(ErrorMessage = "Giờ bắt đầu không được để trống.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giờ kết thúc không được để trống.")]
    public string EndTime { get; set; } = string.Empty;

    /// <summary>Bỏ trống thì mỗi buổi lấy luôn sức chứa của lớp.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa buổi phải là số nguyên dương.")]
    public int? SessionCapacity { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Notes { get; set; }
}

/// <summary>
/// Một đợt lịch của lớp, suy ra từ các buổi đã lập: cùng giáo viên, cùng phòng
/// và cùng ca học thì là một đợt. Lớp dạy tối thứ hai/thứ tư và sáng thứ sáu có
/// hai đợt — gộp lại một dòng thì ca nào cũng bị hiển thị theo giờ của đợt sớm
/// nhất, nên mỗi đợt được trả về riêng.
/// </summary>
public class ClassScheduleBlockViewModel
{
    public string CoachId { get; set; } = string.Empty;

    public string? CoachName { get; set; }

    public int RoomId { get; set; }

    public string? RoomName { get; set; }

    /// <summary>Ngày của buổi đầu và buổi cuối trong đợt này.</summary>
    public string StartDate { get; set; } = string.Empty;

    public string EndDate { get; set; } = string.Empty;

    public string StartTime { get; set; } = string.Empty;

    public string EndTime { get; set; } = string.Empty;

    public List<int> DaysOfWeek { get; set; } = new List<int>();

    public int SessionCount { get; set; }
}

public class ClassViewModel
{
    public int Id { get; set; }

    public int SportId { get; set; }

    public string SportName { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Capacity { get; set; }

    public string Status { get; set; } = string.Empty;

    public int UpcomingSessionCount { get; set; }

    public int TotalSessionCount { get; set; }

    public int ActiveRegistrationCount { get; set; }

    /// <summary>Các đợt lịch của lớp, suy ra từ các buổi đã lập.</summary>
    public List<ClassScheduleBlockViewModel> ScheduleBlocks { get; set; } = new List<ClassScheduleBlockViewModel>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public class CreateClassAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Bộ môn không hợp lệ.")]
    public int SportId { get; set; }

    [Required(ErrorMessage = "Tên lớp không được để trống.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Tên lớp từ 1 đến 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa lớp phải là số nguyên dương.")]
    public int Capacity { get; set; }

    /// <summary>Tạo lớp chỉ cho phép DRAFT hoặc OPEN; lớp mới luôn có buổi học.</summary>
    [RegularExpression("^(DRAFT|OPEN)$", ErrorMessage = "Lớp mới chỉ có thể ở trạng thái nháp hoặc mở đăng ký.")]
    public string? Status { get; set; } = "DRAFT";

    /// <summary>
    /// Lịch học bắt buộc: giáo viên đứng lớp, phòng, ngày bắt đầu, ngày kết
    /// thúc, các thứ trong tuần và khung giờ của buổi học.
    /// </summary>
    [Required(ErrorMessage = "Lịch học không được để trống.")]
    public ClassScheduleAPIViewModel Schedule { get; set; } = new ClassScheduleAPIViewModel();
}

public class UpdateClassAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Bộ môn không hợp lệ.")]
    public int SportId { get; set; }

    [Required(ErrorMessage = "Tên lớp không được để trống.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Tên lớp từ 1 đến 100 ký tự.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự.")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa lớp phải là số nguyên dương.")]
    public int Capacity { get; set; }

    [RegularExpression("^(DRAFT|OPEN|CLOSED|ARCHIVED)$", ErrorMessage = "Trạng thái lớp không hợp lệ.")]
    public string? Status { get; set; }
}

public class UpdateClassStatusAPIViewModel
{
    [Required(ErrorMessage = "Trạng thái không được để trống.")]
    [RegularExpression("^(DRAFT|OPEN|CLOSED|ARCHIVED)$", ErrorMessage = "Trạng thái lớp không hợp lệ.")]
    public string Status { get; set; } = string.Empty;
}

public class ClassCancelImpactViewModel
{
    public string Scope { get; set; } = "CLASS_FUTURE_SESSIONS";

    public int AffectedSessionCount { get; set; }

    public int AffectedRegistrationCount { get; set; }

    public int AffectedMemberCount { get; set; }

    public string? NextSessionStartsAt { get; set; }
}

public class CancelFutureSessionsAPIViewModel
{
    [Required(ErrorMessage = "Lý do hủy không được để trống.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do hủy từ 3 đến 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class CancelFutureSessionsResultViewModel
{
    public int CanceledSessionCount { get; set; }
}

/// <summary>
/// Gán thêm một đợt lịch cho lớp đã có (UC21). Dùng chung khối lịch với lúc tạo
/// lớp, nên màn hình lịch học không cần lập từng buổi một nữa.
/// </summary>
public class AddClassScheduleAPIViewModel : ClassScheduleAPIViewModel
{
}

public class AddClassScheduleResultViewModel
{
    public int ClassId { get; set; }

    public int CreatedSessionCount { get; set; }

    public List<ClassScheduleBlockViewModel> ScheduleBlocks { get; set; } = new List<ClassScheduleBlockViewModel>();
}
