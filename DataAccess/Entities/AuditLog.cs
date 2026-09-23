using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class AuditLog
{
    public string Id { get; set; } = null!;

    public string? AccountId { get; set; }

    public string Action { get; set; } = null!;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account? Account { get; set; }
}
