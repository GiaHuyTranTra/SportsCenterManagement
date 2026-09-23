using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace SportsCenterManagement.Filter
{
    public class AuthFilter : IActionFilter
    {
        private readonly IMemoryCache _cache;
        public AuthFilter(IMemoryCache cache)
        {
            _cache = cache;
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
            throw new NotImplementedException();
        }

        public async void OnActionExecuting(ActionExecutingContext context)
        {
            string? rawToken = await context.HttpContext.GetTokenAsync("access_token");
            string blockedToken = (string)_cache.Get("blacklist-" + rawToken);
            if (!string.IsNullOrEmpty(blockedToken))
            {
                context.Result = new UnauthorizedResult();
                return;
            }
         

        }
    }
}
