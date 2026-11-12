using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class SportsCenterManagementContext
{
    public virtual DbSet<ClassSessionAttendance> ClassSessionAttendances { get; set; }
    public virtual DbSet<CenterVisit> CenterVisits { get; set; }
    public virtual DbSet<SupportRequest> SupportRequests { get; set; }
    public virtual DbSet<MemberTrainingProfile> MemberTrainingProfiles { get; set; }
    public virtual DbSet<TrainingPlan> TrainingPlans { get; set; }
    public virtual DbSet<TrainingResult> TrainingResults { get; set; }
    public virtual DbSet<HomeworkAssignment> HomeworkAssignments { get; set; }
    public virtual DbSet<RolePermissionConfig> RolePermissionConfigs { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClassSessionAttendance>(entity =>
        {
            entity.ToTable("ClassSessionAttendance");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RegistrationId).IsUnique();
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Status).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(300);
            entity.Property(e => e.RecordedBy).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.RecordedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<CenterVisit>(entity =>
        {
            entity.ToTable("CenterVisit");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.CheckedInBy).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.CheckedOutBy).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Note).HasMaxLength(300);
        });

        modelBuilder.Entity<SupportRequest>(entity =>
        {
            entity.ToTable("SupportRequest");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(100).IsUnicode(false);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Subject).HasMaxLength(200);
            entity.Property(e => e.Note).HasMaxLength(1000);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ResolutionNote).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.UpdatedBy).HasMaxLength(400).IsUnicode(false);
        });

        modelBuilder.Entity<MemberTrainingProfile>(entity =>
        {
            entity.ToTable("MemberTrainingProfile");
            entity.HasKey(e => e.MemberId);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.FitnessGoal).HasMaxLength(300);
            entity.Property(e => e.FitnessLevel).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.CurrentWeightKg).HasColumnType("decimal(5, 1)");
            entity.Property(e => e.TargetWeightKg).HasColumnType("decimal(5, 1)");
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.ToTable("TrainingPlan");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CoachId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.PlanType).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Goal).HasMaxLength(300);
            entity.Property(e => e.Homework).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<TrainingResult>(entity =>
        {
            entity.ToTable("TrainingResult");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CoachId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.CompletedExercises).HasMaxLength(500);
            entity.Property(e => e.ProgressRating).HasMaxLength(30).IsUnicode(false);
            entity.Property(e => e.ResultSummary).HasMaxLength(500);
            entity.Property(e => e.CoachFeedback).HasMaxLength(1000);
            entity.Property(e => e.HomeworkAssigned).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<HomeworkAssignment>(entity =>
        {
            entity.ToTable("HomeworkAssignment", table =>
                table.HasCheckConstraint(
                    "CK_HomeworkAssignment_SourceType",
                    "[SourceType] IN ('DIRECT', 'CLASS', 'SESSION')"));
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CoachId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.MemberId).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Instructions).HasMaxLength(2000);
            entity.Property(e => e.SourceType).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.AssignedAtUtc).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.LegacySourceKey).HasMaxLength(100).IsUnicode(false);
            entity.HasIndex(e => new { e.MemberId, e.AssignedAtUtc });
            entity.HasIndex(e => new { e.CoachId, e.AssignedAtUtc });
            entity.HasIndex(e => e.LegacySourceKey)
                .IsUnique()
                .HasFilter("[LegacySourceKey] IS NOT NULL");
        });

        modelBuilder.Entity<RolePermissionConfig>(entity =>
        {
            entity.ToTable("RolePermissionConfig");
            entity.HasKey(e => e.FeatureKey);
            entity.Property(e => e.FeatureKey).HasMaxLength(50).IsUnicode(false);
            entity.Property(e => e.AllowedRolesCsv).HasMaxLength(200).IsUnicode(false);
            entity.Property(e => e.UpdatedBy).HasMaxLength(400).IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())");
        });
    }
}
