using APIViewModel.Auth;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuthService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithCorrectPassword_ReturnsAccountAndResetsFailedCount()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account account = AuthTestData.CreateAccount();
        account.FailedLoginCount = 3;
        context.Add(account);
        await context.SaveChangesAsync();
        AuthService service = CreateAuthService(context);

        LoginResponseAPIViewModel? response = await service.LoginMemberAsync(new LoginRequestAPIViewModel
        {
            Email = account.Email,
            Password = "CorrectPassword123!"
        });

        Assert.NotNull(response);
        Assert.Equal(account.Id, response.Id);
        Assert.Equal(account.Email, response.Email);
        Assert.Equal("Member", response.Role);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_IncrementsFailedLoginCount()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account account = AuthTestData.CreateAccount();
        context.Add(account);
        await context.SaveChangesAsync();
        AuthService service = CreateAuthService(context);

        LoginResponseAPIViewModel? response = await service.LoginMemberAsync(new LoginRequestAPIViewModel
        {
            Email = account.Email,
            Password = "wrong-password"
        });

        Assert.Null(response);
        Assert.Equal(1, account.FailedLoginCount);
        Assert.False(account.IsLocked);
    }

    [Fact]
    public async Task LoginAsync_OnFifthWrongPassword_LocksAccount()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account account = AuthTestData.CreateAccount();
        account.FailedLoginCount = 4;
        context.Add(account);
        await context.SaveChangesAsync();
        AuthService service = CreateAuthService(context);

        await service.LoginMemberAsync(new LoginRequestAPIViewModel
        {
            Email = account.Email,
            Password = "wrong-password"
        });

        Assert.Equal(5, account.FailedLoginCount);
        Assert.True(account.IsLocked);
    }

    [Fact]
    public async Task LoginAsync_WithDeletedAccount_ReturnsAccountInactive()
    {
        await using SportsCenterManagementContext context = CreateContext();
        Account account = AuthTestData.CreateAccount();
        account.DeletedAt = DateTime.UtcNow;
        context.Add(account);
        await context.SaveChangesAsync();
        AuthService service = CreateAuthService(context);

        (LoginResult result, LoginResponseAPIViewModel? response) =
            await service.LoginAsync(new LoginRequestAPIViewModel
            {
                Email = account.Email,
                Password = "CorrectPassword123!"
            });

        Assert.Equal(LoginResult.AccountInactive, result);
        Assert.Null(response);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        DbContextOptions<SportsCenterManagementContext> options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SportsCenterManagementContext(options);
    }

    private static AuthService CreateAuthService(SportsCenterManagementContext context)
    {
        return new AuthService(
            context,
            AuthTestData.CreateAccessTokenService(1440),
            new PasswordHashService());
    }
}
