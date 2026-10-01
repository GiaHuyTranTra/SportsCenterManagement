using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Coach;

public class UpdateManagedCoachAPIViewModel
{
    [MaxLength(100)]
    public string? FullName { get; set; }

    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? WorkSchedule { get; set; }

    public List<int>? DisciplineIds { get; set; }
}
