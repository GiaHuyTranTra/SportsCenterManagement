using System;
using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Member;

public class UpdateMemberAPIViewModel
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    public string? Phone { get; set; }
}
