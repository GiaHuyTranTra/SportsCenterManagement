using System;

namespace APIViewModel;

public class ClassSessionViewDto
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public int SportId { get; set; }
    public string SportName { get; set; } = string.Empty;
    public string CoachId { get; set; } = string.Empty;
    public string CoachName { get; set; } = string.Empty;
    public int RoomId { get; set; }
    public string RoomName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string StartsAt { get; set; } = string.Empty;
    public string EndsAt { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public int BookedCount { get; set; }
    public int RemainingCapacity => Math.Max(0, Capacity - BookedCount);
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class MemberBookedSessionDto : ClassSessionViewDto
{
    public int RegistrationId { get; set; }
    public string RegistrationStatus { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}

public class ClassRosterMemberDto
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public string MemberAccountId { get; set; } = string.Empty;
    public string MemberName { get; set; } = string.Empty;
    public string MemberCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RegisteredVia { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}