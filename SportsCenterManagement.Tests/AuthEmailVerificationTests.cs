using APIViewModel.Auth;
using APIViewModel.Common;
using DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Services.AccountService;
using Services.AuthService;
using Services.EmailVerificationService;
using Services.PasswordHashService;
using SportsCenterManagement.Controllers;

namespace SportsCenterManagement.Tests;

public class AuthEmailVerificationTests
{
    [Theory]
    [InlineData("login", "Member")]
    [InlineData("Login_member", "Member")]
    [InlineData("Login_coach", "Coach")]
    [InlineData("Login_receptionist", "Receptionist")]
    [InlineData("Login_center_manager", "CenterManager")]
    public async Task EveryLoginRoute_RequiresEmailVerification(string route, string role)
    {
        await VerifyLoginAsync(route, role, VerifyEmailCodeResult.CodeRequired);
    }

    [Theory]
    [InlineData("login", "Member")]
    [InlineData("Login_member", "Member")]
    [InlineData("Login_coach", "Coach")]
    [InlineData("Login_receptionist", "Receptionist")]
    [InlineData("Login_center_manager", "CenterManager")]
    public async Task EveryLoginRoute_WithVerifiedEmail_PreservesResponseShape(string route, string role)
    {
        await VerifyLoginAsync(route, role, VerifyEmailCodeResult.Success);
    }

    private static async Task VerifyLoginAsync(string route, string role, VerifyEmailCodeResult verificationResult)
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using SportsCenterManagementContext context = new SportsCenterManagementContext(options);
        string password = Guid.NewGuid().ToString("N");
        Account account = AuthTestData.CreateAccount(password);
        account.Role.Name = role;
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        using MemoryCache cache = new MemoryCache(new MemoryCacheOptions());
        VerificationStub verification = new VerificationStub(verificationResult);
        AuthController controller = new AuthController(
            new AuthService(context, AuthTestData.CreateAccessTokenService(), new PasswordHashService()),
            new AccountService(context, new PasswordHashService()),
            AuthTestData.CreateAccessTokenService(), cache, verification)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        LoginRequestAPIViewModel request = new LoginRequestAPIViewModel
        {
            Email = account.Email,
            Password = password
        };
        IActionResult result = route switch
        {
            "login" => await controller.LoginAsync(request),
            "Login_member" => await controller.LoginMember(request),
            "Login_coach" => await controller.LoginCoach(request),
            "Login_receptionist" => await controller.LoginReceptionist(request),
            _ => await controller.LoginCenterManager(request)
        };
        Assert.Equal(1, verification.Calls);
        if (verificationResult == VerifyEmailCodeResult.Success)
        {
            OkObjectResult response = Assert.IsType<OkObjectResult>(result);
            if (route == "login")
            {
                Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<AuthSessionAPIViewModel>(response.Value).AccessToken));
            }
            else
            {
                Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<string>(response.Value)));
            }
        }
        else
        {
            BadRequestObjectResult response = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("EMAIL_VERIFICATION_REQUIRED",
                Assert.IsType<ApiErrorResponseAPIViewModel>(response.Value).Error.Code);
        }
    }

    private sealed class VerificationStub(VerifyEmailCodeResult result) : IEmailVerificationService
    {
        public int Calls { get; private set; }

        public Task<(RequestEmailVerificationResult Result, int RetryAfterSeconds, string? DemoCode)>
            RequestCodeAsync(string email, string purpose) => throw new NotSupportedException();

        public VerifyEmailCodeResult VerifyCode(string email, string purpose, string? code, bool consumeOnSuccess = true)
        {
            Assert.Equal("LOGIN", purpose);
            Assert.True(consumeOnSuccess);
            Calls++;
            return result;
        }
    }
}
