using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace SportsCenterManagement.Filter
{
    public class AuthFilter : IAsyncActionFilter
    {
        private readonly IMemoryCache _cache;

        public AuthFilter(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task OnActionExecutionAsync(
            ActionExecutingContext context,
            ActionExecutionDelegate next)
        {
            string? rawToken = await context.HttpContext.GetTokenAsync("access_token");
            string? blockedToken = _cache.Get<string>("blacklist-" + rawToken);

            if (!string.IsNullOrEmpty(blockedToken))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            await next();
        }
    }
}
