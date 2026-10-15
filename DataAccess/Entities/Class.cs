using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Class
{
    public int Id { get; set; }

    public int DisciplineId { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    public byte[]? RowVersion { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int Capacity { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Discipline Discipline { get; set; } = null!;

    public virtual Account? CreatedByAccount { get; set; }

    public virtual ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
}
