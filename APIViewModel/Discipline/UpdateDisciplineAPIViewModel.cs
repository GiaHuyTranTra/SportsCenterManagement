using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Discipline;

public class UpdateDisciplineAPIViewModel
{
    [MinLength(2)]
    [MaxLength(80)]
    public string? Name { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
