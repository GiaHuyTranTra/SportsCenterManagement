using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Discipline;

public class UpdateDisciplineStatusAPIViewModel
{
    [Required]
    public bool? IsActive { get; set; }
}
