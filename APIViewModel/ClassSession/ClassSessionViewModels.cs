using System.ComponentModel.DataAnnotations;

namespace APIViewModel.ClassSession;

public class CreateClassSessionAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Huấn luyện viên không được để trống.")]
    public string CoachId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Phòng tập không hợp lệ.")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Ngày diễn ra không được để trống.")]
    public string Date { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giờ bắt đầu không được để trống.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giờ kết thúc không được để trống.")]
    public string EndTime { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa buổi phải là số nguyên dương.")]
    public int Capacity { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Notes { get; set; }
}

public class UpdateClassSessionAPIViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Lớp học không hợp lệ.")]
    public int ClassId { get; set; }

    [Required(ErrorMessage = "Huấn luyện viên không được để trống.")]
    public string CoachId { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Phòng tập không hợp lệ.")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Ngày diễn ra không được để trống.")]
    public string Date { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giờ bắt đầu không được để trống.")]
    public string StartTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Giờ kết thúc không được để trống.")]
    public string EndTime { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Sức chứa buổi phải là số nguyên dương.")]
    public int Capacity { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Notes { get; set; }
}

public class UpdateSessionCoachAPIViewModel
{
    [Required(ErrorMessage = "Huấn luyện viên không được để trống.")]
    public string CoachId { get; set; } = string.Empty;
}

public class CancelSessionAPIViewModel
{
    [Required(ErrorMessage = "Lý do hủy không được để trống.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Lý do hủy từ 3 đến 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class SessionCancelImpactViewModel
{
    public string Scope { get; set; } = "SESSION";

    public int AffectedSessionCount { get; set; } = 1;

    public int AffectedRegistrationCount { get; set; }

    public int AffectedMemberCount { get; set; }

    public string? NextSessionStartsAt { get; set; }
}
