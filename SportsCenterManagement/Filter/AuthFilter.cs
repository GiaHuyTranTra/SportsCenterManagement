using APIViewModel.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Services.AuthService;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace SportsCenterManagement.Filter
{
    public class AuthFilter : IAsyncActionFilter
    {
        private readonly IMemoryCache _cache;
        private readonly IAuthService _authService;

        public AuthFilter(IMemoryCache cache, IAuthService authService)
        {
            _cache = cache;
            _authService = authService;
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

            string? accountId = context.HttpContext.User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(accountId))
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

            if (await _authService.GetSessionAccountAsync(accountId) is null)
            {
                context.Result = new ObjectResult(
                    new ApiErrorResponseAPIViewModel
                    {
                        Success = false,
                        Error = new ApiErrorAPIViewModel
                        {
                            Code = "ACCOUNT_INACTIVE",
                            Message = "This account is not active.",
                            Details = null
                        },
                        TraceId = context.HttpContext.TraceIdentifier
                    })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            await next();
        }
    }
}
