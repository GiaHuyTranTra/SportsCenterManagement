using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Coach
{
    public string AccountId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public string? Specialization { get; set; }

    public string? WorkSchedule { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
