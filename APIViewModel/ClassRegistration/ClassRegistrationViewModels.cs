using System;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.ClassRegistration;

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
