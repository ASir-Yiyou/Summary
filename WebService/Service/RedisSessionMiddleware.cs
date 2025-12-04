using System.Security.Claims;
using AuthenticationServer.Cache;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.Model;
using Summary.Common.Redis.Interfaces;
using Summary.Domain.Interfaces;

namespace AuthenticationServer.Service
{
    public class RedisSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public RedisSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IDbSession<Guid, Guid> session, IRedisCacheProvider cacheProvider, AppDbContext dbContext, IOpenIddictAuthorizationManager authManager)
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
                        sessionData = await cache.GetAsync(cacheKey, async (key) =>
                        {
                            // --- 降级逻辑：Redis 无数据 ---

                            var authId = context.User.FindFirst(OpenIddictConstants.Claims.Private.AuthorizationId)?.Value;

                            if (!string.IsNullOrEmpty(authId))
                            {
                                var authorization = await authManager.FindByIdAsync(authId);

                                if (authorization == null || !await authManager.HasStatusAsync(authorization, OpenIddictConstants.Statuses.Valid))
                                {
                                    return null;
                                }
                            }

                            var user = await dbContext.Users
                                .AsNoTracking()
                                .IgnoreQueryFilters()
                                .FirstOrDefaultAsync(u => u.Id == userId);

                            if (user == null) return null;

                            return new UserSessionCache
                            {
                                UserId = user.Id,
                                TenantId = user.TenantId,
                                // 加载角色...
                            };
                        });
                    }
                    catch (Exception) { /* Log Error */ }

                    if (sessionData != null)
                    {
                        if (session is RequestScopedSession requestSession)
                        {
                            requestSession.Initialize(sessionData.UserId, sessionData.TenantId);
                        }

                        if (sessionData.Roles != null && sessionData.Roles.Any())
                        {
                            var appIdentity = new ClaimsIdentity();
                            foreach (var role in sessionData.Roles)
                            {
                                appIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
                            }
                            context.User.AddIdentity(appIdentity);
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
