using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class ClassSession
{
    public int Id { get; set; }

    public int ClassId { get; set; }

    public string CoachId { get; set; } = null!;

    public int RoomId { get; set; }

    public DateTime SessionDate { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public int Capacity { get; set; }

    public int BookedCount { get; set; }

    public string Status { get; set; } = null!;

    public string? Notes { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CanceledAt { get; set; }

    public string? CanceledBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public byte[]? RowVersion { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<ClassRegistration> Registrations { get; set; } = new List<ClassRegistration>();
}