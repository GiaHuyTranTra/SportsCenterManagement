using APIViewModel.Auth;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Services.AuthService;
using Services.PasswordHashService;

namespace SportsCenterManagement.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task LoginAsync_WithCorrectPassword_ReturnsJwtAndResetsFailedCount()
    {
        await using var context = CreateContext();
        var account = AuthTestData.CreateAccount();
        account.FailedLoginCount = 3;
        context.Add(account);
        await context.SaveChangesAsync();
        var service = CreateAuthService(context);

        var response = await service.LoginAsync(new LoginRequest
        {
            Email = account.Email,
            Password = "CorrectPassword123!"
        });

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal(0, account.FailedLoginCount);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_IncrementsFailedLoginCount()
    {
        await using var context = CreateContext();
        var account = AuthTestData.CreateAccount();
        context.Add(account);
        await context.SaveChangesAsync();
        var service = CreateAuthService(context);

        var response = await service.LoginAsync(new LoginRequest
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
        await using var context = CreateContext();
        var account = AuthTestData.CreateAccount();
        account.FailedLoginCount = 4;
        context.Add(account);
        await context.SaveChangesAsync();
        var service = CreateAuthService(context);

        await service.LoginAsync(new LoginRequest
        {
            Email = account.Email,
            Password = "wrong-password"
        });

        Assert.Equal(5, account.FailedLoginCount);
        Assert.True(account.IsLocked);
    }

    private static SportsCenterManagementContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SportsCenterManagementContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SportsCenterManagementContext(options);
    }

    private static AuthService CreateAuthService(SportsCenterManagementContext context)
    {
        return new AuthService(
            context,
            AuthTestData.CreateAccessTokenService(),
            new PasswordHashService());
    }
}
