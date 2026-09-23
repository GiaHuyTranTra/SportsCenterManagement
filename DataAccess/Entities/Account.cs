using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Account
{
    public string Id { get; set; } = null!;

    public string RoleId { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string Status { get; set; } = null!;

    public int FailedLoginCount { get; set; }

    public bool IsLocked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public virtual Coach? Coach { get; set; }

    public virtual Member? Member { get; set; }

    public virtual Receptionist? Receptionist { get; set; }

    public virtual Role Role { get; set; } = null!;
}
