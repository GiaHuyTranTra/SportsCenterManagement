using System;

namespace DataAccess.Entities;

public class ClassSessionAttendance
{
    public int Id { get; set; }
    public int RegistrationId { get; set; }
    public int SessionId { get; set; }
    public string MemberId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? Note { get; set; }
    public string RecordedBy { get; set; } = null!;
    public DateTime RecordedAt { get; set; }
}

public class CenterVisit
{
    public string Id { get; set; } = null!;
    public string MemberId { get; set; } = null!;
    public DateOnly VisitDate { get; set; }
    public DateTime CheckedInAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public string CheckedInBy { get; set; } = null!;
    public string? CheckedOutBy { get; set; }
    public string? Note { get; set; }
}

public class SupportRequest
{
    public string Id { get; set; } = null!;
    public string MemberId { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string Note { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? ResolutionNote { get; set; }
    public string CreatedBy { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MemberTrainingProfile
{
    public string MemberId { get; set; } = null!;
    public string FitnessGoal { get; set; } = null!;
    public string FitnessLevel { get; set; } = null!;
    public decimal? CurrentWeightKg { get; set; }
    public decimal? TargetWeightKg { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TrainingPlan
{
    public int Id { get; set; }
    public string CoachId { get; set; } = null!;
    public string PlanType { get; set; } = null!;
    public string? MemberId { get; set; }
    public int? ClassId { get; set; }
    public string Title { get; set; } = null!;
    public string Goal { get; set; } = null!;
    public string ExercisesJson { get; set; } = null!;
    public string? Homework { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TrainingResult
{
    public int Id { get; set; }
    public string CoachId { get; set; } = null!;
    public string MemberId { get; set; } = null!;
    public int? SessionId { get; set; }
    public int? PlanId { get; set; }
    public DateOnly RecordedDate { get; set; }
    public string CompletedExercises { get; set; } = null!;
    public int CompletionRate { get; set; }
    public string ProgressRating { get; set; } = null!;
    public string ResultSummary { get; set; } = null!;
    public string CoachFeedback { get; set; } = null!;
    public string? HomeworkAssigned { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RolePermissionConfig
{
    public string FeatureKey { get; set; } = null!;
    public string AllowedRolesCsv { get; set; } = null!;
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
}
