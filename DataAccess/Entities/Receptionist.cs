using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Receptionist
{
    public string AccountId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? WorkShift { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
