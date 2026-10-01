namespace APIViewModel.Coach;

public class CoachDetailAPIViewModel
{
    public string AccountId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string Status { get; set; } = null!;

    public bool IsLocked { get; set; }

    public string FullName { get; set; } = null!;

    public string? WorkSchedule { get; set; }

    public List<CoachDisciplineAPIViewModel> Disciplines { get; set; }
        = new List<CoachDisciplineAPIViewModel>();

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
