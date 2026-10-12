
using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Sport
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Class> Classes { get; set; }
        = new List<Class>();
}
