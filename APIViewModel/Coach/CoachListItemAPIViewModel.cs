using System;

namespace APIViewModel.Coach;

public class CoachListItemAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }
}
