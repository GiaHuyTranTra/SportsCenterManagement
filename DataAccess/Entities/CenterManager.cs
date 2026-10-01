using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class CenterManager
{
    public string AccountId { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
