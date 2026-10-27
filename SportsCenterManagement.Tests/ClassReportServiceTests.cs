using APIViewModel.Report;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.ClassReportService;
using Services.ScheduleQueryService;

namespace SportsCenterManagement.Tests;

public class ClassReportServiceTests
{
    [Fact]
    public async Task Classes_CountBookingsAndDistinctMembersWithinSessionDates()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        (Class first, Class second, int sportId) = await SeedClassesAsync(fixture);
        ClassReportService service = new(fixture.Context, fixture.Configuration);

        ClassReportAPIViewModel report = await service.GetClassReportAsync(new(2026, 10, 1), new(2026, 10, 27), null, null, null, 1, 20);

        Assert.Equal(2, report.TotalClasses);
        Assert.Equal(4, report.TotalSessions);
        Assert.Equal(1, report.CanceledSessions);
        Assert.Equal(4, report.TotalRegistrations);
        Assert.Equal(2, report.UniqueMembers);
        Assert.Equal(3, report.Items[0].RegistrationCount);
        Assert.Equal(2, report.Items[0].UniqueMemberCount);
        Assert.Equal(first.Id, report.Items[0].ClassId);
        Assert.Equal(second.Id, report.Items[1].ClassId);
        Assert.Equal(sportId, report.Items[0].SportId);
        Assert.Equal(report.TotalRegistrations, report.Items.Sum(row => row.RegistrationCount));

        int sqlCount = await fixture.Context.Database.SqlQueryRaw<int>(
            "SELECT COUNT(*) AS Value FROM ClassRegistration r JOIN ClassSession s ON s.Id = r.SessionId WHERE s.SessionDate >= '2026-10-01' AND s.SessionDate < '2026-10-28' AND r.Status = 'BOOKED' AND s.Status <> 'CANCELED'").SingleAsync();
        Assert.Equal(sqlCount, report.TotalRegistrations);
    }

    [Fact]
    public async Task Classes_FiltersStatusAndClassWithSummaryIndependentOfPage()
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        (Class first, Class second, int sportId) = await SeedClassesAsync(fixture);
        ClassReportService service = new(fixture.Context, fixture.Configuration);

        ClassReportAPIViewModel all = await service.GetClassReportAsync(new(2026, 10, 1), new(2026, 10, 27), null, sportId, "ALL", 2, 1);
        ClassReportAPIViewModel canceled = await service.GetClassReportAsync(new(2026, 10, 1), new(2026, 10, 27), first.Id, null, " canceled ", 1, 20);
        ClassReportAPIViewModel empty = await service.GetClassReportAsync(new(2026, 11, 1), new(2026, 11, 2), null, null, null, 1, 20);

        Assert.Equal(6, all.TotalRegistrations);
        Assert.Equal(2, all.TotalPages);
        Assert.Equal(second.Id, Assert.Single(all.Items).ClassId);
        Assert.Equal(2, canceled.TotalRegistrations);
        Assert.Equal(1, canceled.UniqueMembers);
        Assert.Equal(1, canceled.TotalClasses);
        Assert.Equal(0, empty.TotalClasses);
        Assert.Empty(empty.Items);
    }

    [Theory]
    [InlineData(0, null, "BOOKED", 1, 20)]
    [InlineData(null, -1, "BOOKED", 1, 20)]
    [InlineData(null, null, "ATTENDED", 1, 20)]
    [InlineData(null, null, "BOOKED", 0, 20)]
    [InlineData(null, null, "BOOKED", 1, 101)]
    public async Task Classes_RejectInvalidFilters(int? classId, int? sportId, string status, int page, int pageSize)
    {
        await using ReportTestFixture fixture = await ReportTestFixture.CreateAsync();
        ClassReportService service = new(fixture.Context, fixture.Configuration);

        BusinessRuleException error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetClassReportAsync(
            new(2026, 10, 1), new(2026, 10, 27), classId, sportId, status, page, pageSize));

        Assert.Equal("INVALID_CLASS_REPORT_FILTER", error.Code);
    }

    private static async Task<(Class First, Class Second, int SportId)> SeedClassesAsync(ReportTestFixture fixture)
    {
        DateTime created = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        Role coachRole = new() { Id = 2, Name = "Coach" };
        Coach coach = new()
        {
            AccountId = "report-coach", FullName = "Test coach", CreatedAt = created,
            Account = new Account
            {
                Id = "report-coach", Role = coachRole, Email = "report.coach@example.com",
                PasswordHash = "unused", Status = "Active", CreatedAt = created
            }
        };
        Member secondMember = new()
        {
            AccountId = "report-second", MemberCode = "REPORT002", CreatedAt = created,
            Account = new Account
            {
                Id = "report-second", Role = fixture.Member.Account.Role, Email = "report.second@example.com",
                PasswordHash = "unused", Status = "Active", CreatedAt = created
            }
        };
        Discipline sport = new() { Name = "Gym", NormalizedName = "GYM", IsActive = true, CreatedAt = created };
        Room room = new() { Name = "Room", NormalizedName = "ROOM", Capacity = 20, IsActive = true, CreatedAt = created };
        Class first = new() { Name = "A lớp", NormalizedName = "A LỚP", Discipline = sport, Capacity = 20, Status = "OPEN", CreatedAt = created };
        Class second = new() { Name = "B lớp", NormalizedName = "B LỚP", Discipline = sport, Capacity = 20, Status = "ARCHIVED", CreatedAt = created };
        Class outside = new() { Name = "Outside", NormalizedName = "OUTSIDE", Discipline = sport, Capacity = 20, Status = "OPEN", CreatedAt = created };
        ClassSession firstSession = Session(first, coach, room, new(2026, 10, 1), created);
        ClassSession secondSession = Session(first, coach, room, new(2026, 10, 27), created);
        ClassSession thirdSession = Session(second, coach, room, new(2026, 10, 27), created);
        thirdSession.Status = "COMPLETED";
        ClassSession canceledSession = Session(first, coach, room, new(2026, 10, 20), created);
        canceledSession.Status = "CANCELED";
        fixture.Context.ClassSessions.AddRange(firstSession, secondSession, thirdSession, canceledSession,
            Session(outside, coach, room, new(2026, 10, 28), created));
        fixture.Context.ClassRegistrations.AddRange(
            Registration(firstSession, fixture.Member, fixture.Manager, "BOOKED", created),
            Registration(firstSession, secondMember, fixture.Manager, "BOOKED", created),
            Registration(secondSession, fixture.Member, fixture.Manager, "BOOKED", created),
            Registration(thirdSession, fixture.Member, fixture.Manager, "BOOKED", created),
            Registration(firstSession, fixture.Member, fixture.Manager, "CANCELED", created),
            Registration(canceledSession, fixture.Member, fixture.Manager, "CANCELED", created),
            Registration(canceledSession, secondMember, fixture.Manager, "BOOKED", created));
        await fixture.Context.SaveChangesAsync();
        return (first, second, sport.Id);
    }

    private static ClassSession Session(Class currentClass, Coach coach, Room room, DateTime date, DateTime created)
    {
        return new() { Class = currentClass, Coach = coach, Room = room, SessionDate = date,
            StartTime = TimeSpan.FromHours(9), EndTime = TimeSpan.FromHours(10), Capacity = 20,
            Status = "SCHEDULED", CreatedAt = created };
    }

    private static ClassRegistration Registration(ClassSession session, Member member, Account actor, string status, DateTime created)
    {
        return new() { Session = session, Member = member, RegisteredByAccount = actor,
            Status = status, RegisteredVia = "RECEPTIONIST", RegisteredAt = created };
    }
}
