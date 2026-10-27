using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Services.MemberReportService;

namespace SportsCenterManagement.Tests;

public class MemberReportServiceTests
{
    [Fact]
    public async Task Report_CountsMembersOnceUsingServerStatusPrecedence()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        Role role = new() { Id = 3, Name = "Member" };
        Member active = AddMember(context, role, "active");
        Member expired = AddMember(context, role, "expired");
        Member suspended = AddMember(context, role, "suspended");
        Member pending = AddMember(context, role, "pending");
        Member upcoming = AddMember(context, role, "upcoming");
        Member none = AddMember(context, role, "none");
        Member deleted = AddMember(context, role, "deleted");
        deleted.Account.DeletedAt = DateTime.UtcNow;
        Member inactive = AddMember(context, role, "inactive");
        inactive.Account.Status = "Inactive";
        AddSubscription(context, active, "CONFIRMED", new(2026, 10, 1), new(2026, 10, 31));
        AddSubscription(context, active, "CONFIRMED", new(2026, 9, 1), new(2026, 9, 30));
        AddSubscription(context, active, "PENDING_PAYMENT", new(2026, 11, 1), new(2026, 11, 30));
        AddSubscription(context, expired, "CONFIRMED", new(2026, 9, 1), new(2026, 9, 30));
        AddSubscription(context, expired, "PENDING_PAYMENT", new(2026, 10, 1), new(2026, 10, 31));
        AddSubscription(context, suspended, "CONFIRMED", new(2026, 10, 1), new(2026, 10, 31), true);
        AddSubscription(context, pending, "PENDING_PAYMENT", new(2026, 10, 1), new(2026, 10, 31));
        AddSubscription(context, upcoming, "CONFIRMED", new(2026, 11, 1), new(2026, 11, 30));
        AddSubscription(context, none, "CANCELED", new(2026, 10, 1), new(2026, 10, 31));
        AddSubscription(context, inactive, "CONFIRMED", new(2026, 10, 1), new(2026, 10, 31));
        await context.SaveChangesAsync();

        MemberReportAPIViewModel report = await CreateService(context).GetMemberReportAsync(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 27));

        Assert.Equal(7, report.TotalMembers);
        Assert.Equal(7, report.NewMembers);
        Assert.Equal(1, report.ActiveMembers);
        Assert.Equal(1, report.ExpiredMembers);
        Assert.Equal(1, report.SuspendedMembers);
        Assert.Equal(1, report.UpcomingMembers);
        Assert.Equal(1, report.PendingPaymentMembers);
        Assert.Equal(1, report.MembersWithoutSubscription);
        Assert.Equal(1, report.InactiveAccounts);
    }

    [Fact]
    public async Task Report_UsesInclusiveVietnamDaysAndExcludesNextMidnight()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        Role role = new() { Id = 3, Name = "Member" };
        Member before = AddMember(context, role, "before");
        before.CreatedAt = new(2026, 10, 26, 16, 59, 59, DateTimeKind.Utc);
        Member first = AddMember(context, role, "first");
        first.CreatedAt = new(2026, 10, 26, 17, 0, 0, DateTimeKind.Utc);
        Member last = AddMember(context, role, "last");
        last.CreatedAt = new(2026, 10, 27, 16, 59, 59, DateTimeKind.Utc);
        Member after = AddMember(context, role, "after");
        after.CreatedAt = new(2026, 10, 27, 17, 0, 0, DateTimeKind.Utc);
        await context.SaveChangesAsync();

        MemberReportAPIViewModel report = await CreateService(context).GetMemberReportAsync(new(2026, 10, 27), new(2026, 10, 27));

        Assert.Equal(3, report.TotalMembers);
        Assert.Equal(2, report.NewMembers);
    }

    [Fact]
    public async Task Report_DefaultsToCenterDateAndRejectsReversedRange()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        await using SportsCenterManagementContext context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        MemberReportService service = CreateService(context);

        MemberReportAPIViewModel report = await service.GetMemberReportAsync(null, null);

        Assert.Equal(new DateOnly(2026, 10, 28), report.AsOfDate);
        Assert.Equal(0, report.TotalMembers);
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetMemberReportAsync(new(2026, 10, 29), new(2026, 10, 28)));
    }

    private static SportsCenterManagementContext CreateContext(SqliteConnection connection)
    {
        DbContextOptions<SportsCenterManagementContext> options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseSqlite(connection).Options;
        return new(options);
    }

    private static MemberReportService CreateService(SportsCenterManagementContext context)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["BusinessSettings:TimeZoneId"] = "SE Asia Standard Time" }).Build();
        return new(context, configuration, new FixedClock());
    }

    private static Member AddMember(SportsCenterManagementContext context, Role role, string id)
    {
        DateTime createdAt = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        Account account = new()
        {
            Id = id, Email = id + "@example.com", PasswordHash = "unused", Status = "Active",
            Role = role, CreatedAt = createdAt
        };
        Member member = new() { Account = account, AccountId = id, MemberCode = id, CreatedAt = createdAt };
        context.Members.Add(member);
        return member;
    }

    private static void AddSubscription(SportsCenterManagementContext context, Member member, string status,
        DateOnly start, DateOnly end, bool suspended = false)
    {
        MembershipPackage package = new()
        {
            Name = Guid.NewGuid().ToString(), Benefits = "[]", CreatedAt = member.CreatedAt, Price = 100000, DurationMonths = 1
        };
        context.MemberSubscriptions.Add(new MemberSubscription
        {
            Member = member, Package = package, PackageName = "Monthly", Benefits = "[]", DurationMonths = 1,
            Status = status, Kind = "REGISTER", StartDate = start, EndDate = end, IsSuspended = suspended,
            CreatedAt = member.CreatedAt
        });
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 27, 17, 0, 0, TimeSpan.Zero);
    }
}
