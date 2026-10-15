using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Room
{
    public int Id { get; set; }

    public string NormalizedName { get; set; } = string.Empty;

    public byte[]? RowVersion { get; set; }

    public string Name { get; set; } = null!;

    public string? Location { get; set; }

    public string? RoomType { get; set; }

    public int Capacity { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ClassSession> ClassSessions { get; set; } = new List<ClassSession>();
}