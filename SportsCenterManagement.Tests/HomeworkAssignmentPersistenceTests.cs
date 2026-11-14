using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace SportsCenterManagement.Tests;

public sealed class HomeworkAssignmentPersistenceTests
{
    [Fact]
    public void Model_UsesRequiredLengthsAndHistoryIndexes()
    {
        using SportsCenterManagementContext context = CreateContext();
        IEntityType entity = context.Model.FindEntityType(typeof(HomeworkAssignment))!;

        Assert.Equal(400, entity.FindProperty(nameof(HomeworkAssignment.CoachId))!.GetMaxLength());
        Assert.Equal(400, entity.FindProperty(nameof(HomeworkAssignment.MemberId))!.GetMaxLength());
        Assert.Equal(200, entity.FindProperty(nameof(HomeworkAssignment.Title))!.GetMaxLength());
        Assert.Equal(2000, entity.FindProperty(nameof(HomeworkAssignment.Instructions))!.GetMaxLength());
        Assert.Equal(20, entity.FindProperty(nameof(HomeworkAssignment.SourceType))!.GetMaxLength());
        Assert.Equal(100, entity.FindProperty(nameof(HomeworkAssignment.LegacySourceKey))!.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(HomeworkAssignment.CoachId))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(HomeworkAssignment.MemberId))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(HomeworkAssignment.Title))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(HomeworkAssignment.Instructions))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(HomeworkAssignment.SourceType))!.IsNullable);

        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(HomeworkAssignment.MemberId), nameof(HomeworkAssignment.AssignedAtUtc)]));
        Assert.Contains(entity.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(HomeworkAssignment.CoachId), nameof(HomeworkAssignment.AssignedAtUtc)]));
    }

    [Fact]
    public async Task ClassAndSessionContext_AreNullable()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.HomeworkAssignments.Add(Assignment("legacy-null-context"));

        await context.SaveChangesAsync();

        HomeworkAssignment saved = await context.HomeworkAssignments.SingleAsync();
        Assert.Null(saved.ClassId);
        Assert.Null(saved.SessionId);
    }

    [Fact]
    public async Task LegacySourceKey_IsUniqueWhenPresent()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        context.HomeworkAssignments.AddRange(
            Assignment("RESULT:42"),
            Assignment("RESULT:42"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static HomeworkAssignment Assignment(string? legacySourceKey) => new()
    {
        CoachId = "coach-1",
        MemberId = "member-1",
        Title = "Bài tập sau buổi tập",
        Instructions = "Thực hiện 3 hiệp plank.",
        SourceType = "DIRECT",
        AssignedAtUtc = DateTime.UtcNow,
        LegacySourceKey = legacySourceKey
    };

    private static SportsCenterManagementContext CreateContext(SqliteConnection? connection = null) =>
        new(new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseSqlite(connection ?? new SqliteConnection("Data Source=:memory:"))
            .Options);
}
