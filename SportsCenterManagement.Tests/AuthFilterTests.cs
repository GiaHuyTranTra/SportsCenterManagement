using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataAccess.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Services.AuthService;
using Services.PasswordHashService;
using SportsCenterManagement.Filter;

namespace SportsCenterManagement.Tests;

public class AuthFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WithDeletedAccountToken_ReturnsForbidden()
    {
        DbContextOptions<SportsCenterManagementContext> options =
            new DbContextOptionsBuilder<SportsCenterManagementContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
        await using SportsCenterManagementContext database =
            new SportsCenterManagementContext(options);
        Account account = AuthTestData.CreateAccount();
        account.DeletedAt = DateTime.UtcNow;
        database.Add(account);
        await database.SaveChangesAsync();
        AuthService authService = new AuthService(
            database,
            AuthTestData.CreateAccessTokenService(),
            new PasswordHashService());
        using MemoryCache cache = new MemoryCache(Options.Create(new MemoryCacheOptions()));
        AuthFilter filter = new AuthFilter(cache, authService);
        DefaultHttpContext httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, account.Id),
                    new Claim(JwtRegisteredClaimNames.Jti, "token-id")
                },
                "Test"))
        };
        ActionContext actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());
        ActionExecutingContext executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
        bool actionExecuted = false;
        ActionExecutionDelegate next = () =>
        {
            actionExecuted = true;
            return Task.FromResult(new ActionExecutedContext(
                actionContext,
                new List<IFilterMetadata>(),
                new object()));
        };

        await filter.OnActionExecutionAsync(executingContext, next);

        ObjectResult result = Assert.IsType<ObjectResult>(executingContext.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(actionExecuted);
    }
}
