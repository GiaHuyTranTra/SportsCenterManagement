using System;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Class;

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

    [RegularExpression("^(DRAFT|OPEN|CLOSED|ARCHIVED)$", ErrorMessage = "Trạng thái lớp không hợp lệ.")]
    public string? Status { get; set; } = "DRAFT";
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
