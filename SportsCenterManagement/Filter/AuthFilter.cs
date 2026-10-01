using APIViewModel.Common;
using DataAccess.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SportsCenterManagement.Filter
{
    public class AuthFilter : IAsyncActionFilter
    {
        private readonly IMemoryCache _cache;
        private readonly SportsCenterManagementContext _context;

        public AuthFilter(
            IMemoryCache cache,
            SportsCenterManagementContext context)
        {
            _cache = cache;
            _context = context;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            Microsoft.AspNetCore.Http.Endpoint? endpoint =
                context.HttpContext.GetEndpoint();
            IAllowAnonymous? allowAnonymous =
                endpoint?.Metadata.GetMetadata<IAllowAnonymous>();

            if (allowAnonymous is not null)
            {
                await next();
                return;
            }

            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                await next();
                return;
            }

            string? jti = context.HttpContext.User
                .FindFirstValue(JwtRegisteredClaimNames.Jti);

            if (string.IsNullOrWhiteSpace(jti))
            {
                context.Result = new UnauthorizedObjectResult(
                    new ApiErrorResponseAPIViewModel
                    {
                        Success = false,
                        Error = new ApiErrorAPIViewModel
                        {
                            Code = "INVALID_TOKEN_CLAIMS",
                            Message = "The access token does not contain the required claims.",
                            Details = null
                        },
                        TraceId = context.HttpContext.TraceIdentifier
                    });
                return;
            }

            string cacheKey = "jwt:blacklist:" + jti;

            if (_cache.TryGetValue(cacheKey, out bool _))
            {
                context.Result = new UnauthorizedObjectResult(
                    new ApiErrorResponseAPIViewModel
                    {
                        Success = false,
                        Error = new ApiErrorAPIViewModel
                        {
                            Code = "TOKEN_REVOKED",
                            Message = "The access token has been revoked.",
                            Details = null
                        },
                        TraceId = context.HttpContext.TraceIdentifier
                    });
                return;
            }

            ControllerActionDescriptor? actionDescriptor =
                context.ActionDescriptor as ControllerActionDescriptor;
            bool isAuthenticationUtilityAction = actionDescriptor is not null &&
                actionDescriptor.ControllerName == "Auth" &&
                (actionDescriptor.ActionName == "Logout" ||
                 actionDescriptor.ActionName == "CheckToken");
            if (isAuthenticationUtilityAction)
            {
                await next();
                return;
            }

            string? accountId = context.HttpContext.User
                .FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(accountId))
            {
                context.Result = CreateErrorResult(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "INVALID_TOKEN_CLAIMS",
                    "The access token does not contain the required claims.");
                return;
            }

            Account? account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == accountId);
            if (account is null)
            {
                context.Result = CreateErrorResult(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "ACCOUNT_NOT_FOUND",
                    "The authenticated account no longer exists.");
                return;
            }

            if (account.DeletedAt is not null ||
                !string.Equals(account.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = CreateErrorResult(
                    context,
                    StatusCodes.Status403Forbidden,
                    "ACCOUNT_INACTIVE",
                    "This account is not active.");
                return;
            }

            if (account.IsLocked)
            {
                context.Result = CreateErrorResult(
                    context,
                    StatusCodes.Status423Locked,
                    "ACCOUNT_LOCKED",
                    "This account is locked.");
                return;
            }

            await next();
        }

        private static ObjectResult CreateErrorResult(
            ActionExecutingContext context,
            int statusCode,
            string code,
            string message)
        {
            return new ObjectResult(new ApiErrorResponseAPIViewModel
            {
                Success = false,
                Error = new ApiErrorAPIViewModel
                {
                    Code = code,
                    Message = message,
                    Details = null
                },
                TraceId = context.HttpContext.TraceIdentifier
            })
            {
                StatusCode = statusCode
            };
        }
    }
}
