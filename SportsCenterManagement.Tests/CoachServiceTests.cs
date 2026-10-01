using APIViewModel.Coach;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuditLogService;
using Services.CoachService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class CoachServiceTests
{
    [Fact]
    public async Task CreateCoachAsync_CreatesAssignmentsAndAuditLog()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role managerRole = new Role { Id = 1, Name = "CenterManager" };
        Role coachRole = new Role { Id = 2, Name = "Coach" };
        Account manager = CreateManager(managerRole);
        Discipline fitness = CreateDiscipline(1, "Fitness", true);
        Discipline yoga = CreateDiscipline(2, "Yoga", true);
        context.AddRange(managerRole, coachRole, manager, fitness, yoga);
        await context.SaveChangesAsync();
        PasswordHashService passwordHashService = new PasswordHashService();
        CoachService service = new CoachService(
            context,
            passwordHashService,
            new AuditLogService(context));
        CreateManagedCoachAPIViewModel request = new CreateManagedCoachAPIViewModel
        {
            Email = " COACH.NEW@EXAMPLE.COM ",
            Password = "Password@123",
            FullName = " New Coach ",
            Phone = "0912345678",
            WorkSchedule = " Monday to Friday ",
            DisciplineIds = new List<int> { fitness.Id, yoga.Id }
        };

        (CreateCoachResult Result, CoachDetailAPIViewModel? Data) result =
            await service.CreateCoachAsync(manager.Id, request);

        Assert.Equal(CreateCoachResult.Success, result.Result);
        Assert.NotNull(result.Data);
        Account createdAccount = await context.Accounts
            .Include(item => item.Coach)
                .ThenInclude(coach => coach!.CoachDisciplines)
            .SingleAsync(item => item.Email == "coach.new@example.com");
        Assert.True(passwordHashService.VerifyPassword("Password@123", createdAccount.PasswordHash));
        Assert.Equal(2, createdAccount.Coach!.CoachDisciplines.Count);
        Assert.Equal("Fitness, Yoga", createdAccount.Coach.Specialization);
        Assert.Single(await context.AuditLogs.Where(item => item.EntityId == createdAccount.Id).ToListAsync());
    }

    [Fact]
    public async Task UpdateCoachAsync_PreservesInactiveAssignmentAndReplacesActiveAssignment()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role managerRole = new Role { Id = 1, Name = "CenterManager" };
        Role coachRole = new Role { Id = 2, Name = "Coach" };
        Account manager = CreateManager(managerRole);
        Discipline oldActive = CreateDiscipline(1, "Fitness", true);
        Discipline historicalInactive = CreateDiscipline(2, "Boxing", false);
        Discipline replacement = CreateDiscipline(3, "Yoga", true);
        Account coachAccount = CreateCoachAccount(coachRole, oldActive, historicalInactive);
        context.AddRange(
            managerRole,
            coachRole,
            manager,
            oldActive,
            historicalInactive,
            replacement,
            coachAccount);
        await context.SaveChangesAsync();
        CoachService service = new CoachService(
            context,
            new PasswordHashService(),
            new AuditLogService(context));

        (UpdateCoachResult Result, CoachDetailAPIViewModel? Data) result =
            await service.UpdateCoachAsync(
                manager.Id,
                coachAccount.Id,
                new UpdateManagedCoachAPIViewModel
                {
                    DisciplineIds = new List<int> { replacement.Id }
                });

        Assert.Equal(UpdateCoachResult.Success, result.Result);
        Assert.NotNull(result.Data);
        List<int> persistedIds = await context.CoachDisciplines
            .Where(item => item.CoachAccountId == coachAccount.Id)
            .OrderBy(item => item.DisciplineId)
            .Select(item => item.DisciplineId)
            .ToListAsync();
        Assert.Equal(new List<int> { historicalInactive.Id, replacement.Id }, persistedIds);
        Assert.DoesNotContain(result.Data.Disciplines, item => item.DisciplineId == oldActive.Id);
        Assert.Contains(result.Data.Disciplines, item =>
            item.DisciplineId == historicalInactive.Id && !item.IsActive);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static Account CreateManager(Role role)
    {
        Account account = new Account
        {
            Id = "manager-1",
            Email = "manager@example.com",
            PasswordHash = "unused",
            Status = "Active",
            RoleId = role.Id,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        account.CenterManager = new CenterManager
        {
            AccountId = account.Id,
            Account = account,
            FullName = "Manager One",
            CreatedAt = DateTime.UtcNow
        };
        return account;
    }

    private static Account CreateCoachAccount(
        Role role,
        Discipline activeDiscipline,
        Discipline inactiveDiscipline)
    {
        Account account = new Account
        {
            Id = "coach-1",
            Email = "coach@example.com",
            Phone = "0900000001",
            PasswordHash = "unused",
            Status = "Active",
            RoleId = role.Id,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        Coach coach = new Coach
        {
            AccountId = account.Id,
            Account = account,
            FullName = "Coach One",
            Specialization = "Boxing, Fitness",
            CreatedAt = DateTime.UtcNow
        };
        account.Coach = coach;
        coach.CoachDisciplines.Add(CreateLink(coach, activeDiscipline));
        coach.CoachDisciplines.Add(CreateLink(coach, inactiveDiscipline));
        return account;
    }

    private static CoachDiscipline CreateLink(Coach coach, Discipline discipline)
    {
        return new CoachDiscipline
        {
            CoachAccountId = coach.AccountId,
            CoachAccount = coach,
            DisciplineId = discipline.Id,
            Discipline = discipline,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static Discipline CreateDiscipline(int id, string name, bool isActive)
    {
        return new Discipline
        {
            Id = id,
            Name = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
    }
}
