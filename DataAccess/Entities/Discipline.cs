using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Discipline
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CoachDiscipline> CoachDisciplines { get; set; }
        = new List<CoachDiscipline>();
}
