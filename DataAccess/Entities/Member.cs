using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class Member
{
    public string AccountId { get; set; } = null!;

    public string MemberCode { get; set; } = null!;

    public string? FullName { get; set; }

    public string? Phone { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string? AvatarUrl { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
