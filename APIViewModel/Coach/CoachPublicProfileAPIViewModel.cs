namespace APIViewModel.Coach;

public sealed class CoachPublicProfileAPIViewModel
{
    public string AccountId { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? WorkSchedule { get; set; }
    public List<CoachPublicDisciplineAPIViewModel> Disciplines { get; set; } = new();
}

public sealed class CoachPublicDisciplineAPIViewModel
{
    public int DisciplineId { get; set; }
    public string DisciplineName { get; set; } = null!;
}
