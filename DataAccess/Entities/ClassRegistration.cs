using System;

namespace DataAccess.Entities;

public partial class ClassRegistration
{
    public int Id { get; set; }

    public int? MemberSubscriptionId { get; set; }

    public byte[]? RowVersion { get; set; }

    public int SessionId { get; set; }

    public string MemberId { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime RegisteredAt { get; set; }

    public string RegisteredVia { get; set; } = null!;

    public string RegisteredBy { get; set; } = null!;

    public DateTime? CanceledAt { get; set; }

    public string? CanceledBy { get; set; }

    public string? CancelReason { get; set; }

    public virtual ClassSession Session { get; set; } = null!;

    public virtual Member Member { get; set; } = null!;

    public virtual Account RegisteredByAccount { get; set; } = null!;

    public virtual Account? CanceledByAccount { get; set; }
}