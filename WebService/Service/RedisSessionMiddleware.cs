using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OpenIddict.Abstractions;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.Model;
using Summary.Common.Redis.Interfaces;
using Summary.Domain.Interfaces;
using WebService.Cache;

namespace WebService.Service
{
    public class RedisSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public RedisSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IDbSession<Guid, Guid> session, IRedisCacheProvider cacheProvider,AppDbContext dbContext)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userIdString = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                   ?? context.User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

                if (!string.IsNullOrEmpty(userIdString) && Guid.TryParse(userIdString, out var userId))
                {
                    var cache = cacheProvider.GetRedisCache("User");
                    var cacheKey = userId.ToString();

                    UserSessionCache? sessionData = null;

                    try
                    {

                        sessionData = await cache.GetAsync<UserSessionCache?>(cacheKey, async (key) =>
                        {

                            var user = await dbContext.Users
                                .AsNoTracking()
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(u => u.Id == userId);

                            if (user == null) return null;

                            return new UserSessionCache
                            {
                                UserId = user.Id,
                                TenantId = user.TenantId
                            };
                        });
                    }
                    catch (Exception){ }

                    if (sessionData != null)
                    {
                        if (session is RequestScopedSession requestSession)
                        {
                            requestSession.Initialize(sessionData.UserId, sessionData.TenantId);
                        }
                    }
                    else
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
