using System;
using System.Collections.Generic;

namespace DataAccess.Entities;

public partial class PasswordChangeOtp
{
    public int Id { get; set; }

    public string AccountId { get; set; } = null!;

    public string OtpHash { get; set; } = null!;

    public int FailedAttempts { get; set; }

    public int MaxAttempts { get; set; }

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Account Account { get; set; } = null!;
}
