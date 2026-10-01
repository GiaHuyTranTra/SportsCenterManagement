using System.ComponentModel.DataAnnotations;

namespace APIViewModel.Discipline;

public class CreateDisciplineAPIViewModel
{
    [Required]
    [MinLength(2)]
    [MaxLength(80)]
    public string Name { get; set; } = null!;

    [MaxLength(500)]
    public string? Description { get; set; }
}
