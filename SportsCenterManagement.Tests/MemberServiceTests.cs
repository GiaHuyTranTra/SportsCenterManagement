using APIViewModel.Member;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.MemberService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class MemberServiceTests
{
    [Fact]
    public async Task CreateManagedMemberAsync_GeneratesCodeAndHashedPassword()
    {
        await using SportsCenterManagementContext context = CreateContext();
        context.Roles.Add(CreateMemberRole());
        await context.SaveChangesAsync();
        PasswordHashService passwordHashService = new PasswordHashService();
        MemberService service = new MemberService(context, passwordHashService);

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(CreateRequest());

        Assert.Equal(CreateManagedMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.StartsWith("MB", data.Member.MemberCode);
        Account savedAccount = await context.Accounts.SingleAsync();
        Assert.True(passwordHashService.VerifyPassword(data.InitialPassword, savedAccount.PasswordHash));
    }

    [Fact]
    public async Task CreateManagedMemberAsync_WithDuplicateEmail_WritesNothing()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("existing-account", "MB001", "existing@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);
        CreateManagedMemberAPIViewModel request = CreateRequest();
        request.Email = " EXISTING@example.com ";

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(request);

        Assert.Equal(CreateManagedMemberResult.DuplicateEmail, result);
        Assert.Null(data);
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task CreateManagedMemberAsync_WithDuplicatePhone_WritesNothing()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("existing-account", "MB001", "existing@example.com", "0912345678", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (CreateManagedMemberResult result, CreateManagedMemberResponseAPIViewModel? data) =
            await service.CreateManagedMemberAsync(CreateRequest());

        Assert.Equal(CreateManagedMemberResult.DuplicatePhone, result);
        Assert.Null(data);
        Assert.Equal(1, await context.Accounts.CountAsync());
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task UpdateManagedMemberAsync_NormalizesAccountAndProfileFields()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "old@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (UpdateManagedMemberResult result, MemberDetailAPIViewModel? data) =
            await service.UpdateManagedMemberAsync(
                "member-account",
                new UpdateManagedMemberAPIViewModel
                {
                    FullName = "  Long Updated  ",
                    Email = "  LONG.UPDATED@EXAMPLE.COM  ",
                    Phone = " 0987654321 ",
                    DateOfBirth = new DateOnly(2001, 2, 3),
                    IsActive = false
                });

        Assert.Equal(UpdateManagedMemberResult.Success, result);
        Assert.NotNull(data);
        Assert.Equal("Long Updated", data.FullName);
        Assert.Equal("long.updated@example.com", data.Email);
        Assert.Equal("0987654321", data.Phone);
        Assert.Equal("Inactive", data.Status);
    }

    [Fact]
    public async Task UpdateManagedMemberAsync_WithAnotherAccountsEmail_ReturnsDuplicateEmail()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "member@example.com", "0900000001", memberRole));
        context.Add(CreateMember("other-account", "MB002", "other@example.com", "0900000002", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        (UpdateManagedMemberResult result, MemberDetailAPIViewModel? data) =
            await service.UpdateManagedMemberAsync(
                "member-account",
                new UpdateManagedMemberAPIViewModel
                {
                    FullName = "Member Name",
                    Email = "OTHER@example.com",
                    Phone = "0900000001",
                    DateOfBirth = new DateOnly(2000, 1, 1),
                    IsActive = true
                });

        Assert.Equal(UpdateManagedMemberResult.DuplicateEmail, result);
        Assert.Null(data);
        Assert.Equal("member@example.com", (await context.Accounts.FindAsync("member-account"))?.Email);
    }

    [Fact]
    public async Task SoftDeleteMemberAsync_RetainsMemberAndMarksAccountDeleted()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("member-account", "MB001", "member@example.com", "0900000001", memberRole));
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        DeleteMemberResult result = await service.SoftDeleteMemberAsync("member-account");

        Assert.Equal(DeleteMemberResult.Success, result);
        Account account = await context.Accounts.SingleAsync();
        Assert.Equal("Inactive", account.Status);
        Assert.NotNull(account.DeletedAt);
        Assert.Equal(1, await context.Members.CountAsync());
    }

    [Fact]
    public async Task MemberQueries_ExcludeSoftDeletedAccounts()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        context.Add(CreateMember("active-account", "MB001", "active@example.com", "0900000001", memberRole));
        Account deleted = CreateMember("deleted-account", "MB002", "deleted@example.com", "0900000002", memberRole);
        deleted.DeletedAt = DateTime.UtcNow;
        deleted.Status = "Inactive";
        context.Add(deleted);
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        PagedMemberResultAPIViewModel list = await service.GetMembersAsync(1, 20, null, null);
        MemberDetailAPIViewModel? detail = await service.GetMemberByIdAsync("deleted-account");
        List<MemberSearchAPIViewModel> search = await service.QuickSearchMembersAsync("deleted");

        Assert.Single(list.Items);
        Assert.Equal("active-account", list.Items[0].AccountId);
        Assert.Null(detail);
        Assert.Empty(search);
    }

    [Fact]
    public async Task GetMembersAsync_SearchesMemberCodeAndCapsPageSizeAtTwenty()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Role memberRole = CreateMemberRole();
        for (int index = 1; index <= 25; index++)
        {
            context.Add(CreateMember(
                $"account-{index}",
                $"MB{index:000}",
                $"person{index}@example.com",
                $"09{index:00000000}",
                memberRole));
        }
        await context.SaveChangesAsync();
        MemberService service = CreateService(context);

        PagedMemberResultAPIViewModel result = await service.GetMembersAsync(1, 50, "MB", null);

        Assert.Equal(25, result.TotalItems);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(20, result.Items.Count);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        return new SportsCenterManagementContext(options);
    }

    private static MemberService CreateService(SportsCenterManagementContext context)
    {
        return new MemberService(context, new PasswordHashService());
    }

    private static Role CreateMemberRole()
    {
        return new Role { Id = 3, Name = "Member" };
    }

    private static CreateManagedMemberAPIViewModel CreateRequest()
    {
        return new CreateManagedMemberAPIViewModel
        {
            FullName = "Long Test Member",
            Email = "long.member@example.com",
            Phone = "0912345678",
            DateOfBirth = new DateOnly(2000, 1, 2),
            IsActive = true
        };
    }

    private static Account CreateMember(
        string accountId,
        string memberCode,
        string email,
        string phone,
        Role role)
    {
        DateTime createdAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);
        return new Account
        {
            Id = accountId,
            Email = email,
            Phone = phone,
            PasswordHash = "unused",
            Status = "Active",
            CreatedAt = createdAt,
            RoleId = role.Id,
            Role = role,
            Member = new Member
            {
                AccountId = accountId,
                MemberCode = memberCode,
                FullName = "Member Name",
                DateOfBirth = new DateOnly(2000, 1, 1),
                CreatedAt = createdAt
            }
        };
    }
}
