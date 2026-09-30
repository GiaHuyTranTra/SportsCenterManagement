using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Account;

public class UpdateProfileAPIViewModel
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(255)]
    public string? Specialization { get; set; }

    [MaxLength(500)]
    public string? WorkSchedule { get; set; }

    [MaxLength(100)]
    public string? WorkShift { get; set; }
}
