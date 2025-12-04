using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.Model;
using Summary.Common.Redis.Interfaces;
using Summary.Domain.Dtos;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace AuthenticationServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IRedisCacheProvider _redisProvider;
        private readonly IDbSession<Guid, Guid> _currentUserSession;
        private readonly IOpenIddictAuthorizationManager _authorizationManager;

        public AuthController(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IRedisCacheProvider redisProvider,
            IDbSession<Guid, Guid> currentUserSession,
            IOpenIddictAuthorizationManager authorizationManager)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _redisProvider = redisProvider;
            _currentUserSession = currentUserSession;
            _authorizationManager = authorizationManager;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest("Username and password are required.");

            var exists = await _context.Users
                .IgnoreQueryFilters()// 因为注册是匿名操作，此时 Session.TenantId 为空，如果不忽略过滤器，会因为 TenantId 不匹配而查不到已存在的用户，导致重名注册。
                .AnyAsync(u => u.Username == request.Username);

            if (exists)
                return Conflict(new { Message = "Username already exists." });

            // 2. 创建用户实体
            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                Email = request.Email,
                TenantId = request.TenantId == Guid.Empty ? request.TenantId : Guid.NewGuid(),
                MainGroupId = request.GroupId,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            // 3. 加密密码
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

            // 4. 保存到数据库
            // 注意：如果你的 TestData 初始化中没有创建对应的 Tenant 或 Group，这里可能会报错（外键约束）
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Username, Message = "Registration successful." });
        }

        // OpenIddict 会自动拦截指向 /connect/token 的请求并路由到这里
        [HttpPost("~/connect/token")]
        [IgnoreAntiforgeryToken]
        [EnableRateLimiting("LoginPolicy")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest();
            if (request == null) throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            if (request.IsClientCredentialsGrantType())
            {
                // 机器不需要查 User 表，不需要写 Redis Session
                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // Subject 就是 ClientId
                identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId ?? "machine");
                identity.AddClaim(OpenIddictConstants.Claims.Name, request.ClientId ?? "machine");

                var principal = new ClaimsPrincipal(identity);
                principal.SetScopes(request.GetScopes()); // 这里只有 api

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsPasswordGrantType())
            {
                // 1. 查用户 (忽略过滤器)
                var user = await _context.Users.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                // 2. 验密
                if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
                {
                    return InvalidUserOrPassword();
                }

                await WriteToRedisAsync(user);

                // 4. 建凭证
                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString()); // User Id
                identity.AddClaim(OpenIddictConstants.Claims.Name, user.Username);

                var principal = new ClaimsPrincipal(identity);

                principal.SetScopes(request.GetScopes());

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            // 场景 B: Token 过期了，使用 Refresh Token 换新 (刷新模式)
            else if (request.IsRefreshTokenGrantType())
            {
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                var userId = result.Principal!.GetClaim(OpenIddictConstants.Claims.Subject);

                if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var uid)) return Forbid();

                var user = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == uid);

                if (user == null) return Forbid(); // 用户可能已被物理删除

                await WriteToRedisAsync(user);

                var principal = CreatePrincipal(user, request.GetScopes());

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
        }

        private ClaimsPrincipal CreatePrincipal(User user, IEnumerable<string> scopes)
        {
            var identity = new ClaimsIdentity(
                authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            // 1. 添加必要 Claim
            identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());
            identity.AddClaim(OpenIddictConstants.Claims.Name, user.Username);

            var principal = new ClaimsPrincipal(identity);

            // 如果请求包含了 offline_access，OpenIddict 才会颁发 Refresh Token
            principal.SetScopes(scopes);
            principal.SetAccessTokenLifetime(TimeSpan.FromMinutes(30));
            principal.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

            foreach (var claim in principal.Claims)
            {
                var destinations = new List<string> { OpenIddictConstants.Destinations.AccessToken };

                // 如果是某些特殊 Claim，也允许放入 Identity Token (前端可见)
                if (claim.Type == OpenIddictConstants.Claims.Name ||
                    claim.Type == OpenIddictConstants.Claims.Subject)
                {
                    destinations.Add(OpenIddictConstants.Destinations.IdentityToken);
                }

                claim.SetDestinations(destinations);
            }

            return principal;
        }

        private async Task WriteToRedisAsync(User user)
        {
            var cache = _redisProvider.GetRedisCache("User");
            var sessionData = new UserSessionCache
            {
                UserId = user.Id,
                TenantId = user.TenantId
            };

            await cache.SetAsync(
                user.Id.ToString(),
                sessionData,
                slidingExpireTime: TimeSpan.FromHours(2)
            );
        }

        [HttpPost("change-password")]
        [Authorize] // 必须携带有效 Token
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto request)
        {
            var userId = _currentUserSession.UserId;
            if (userId == Guid.Empty) return Unauthorized();
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound("User not found.");

            var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.OldPassword);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                return BadRequest(new { Message = "Incorrect old password." });
            }

            user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);

            user.SecurityStamp = Guid.NewGuid().ToString();
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Password changed successfully." });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var authorizationId = User.GetAuthorizationId();

            if (!string.IsNullOrEmpty(authorizationId))
            {
                var authorization = await _authorizationManager.FindByIdAsync(authorizationId);

                if (authorization != null)
                {
                   var rlt = await _authorizationManager.TryRevokeAsync(authorization);
                }
            }

            var userId = _currentUserSession.UserId;

            if (userId != Guid.Empty)
            {
                var cache = _redisProvider.GetRedisCache("User");
                await cache.RemoveAsync(userId.ToString());
            }
            return Ok(new { Message = "Logged out successfully." });
        }

        #region Cookie

        //[HttpPost("login-cookie")]
        //[AllowAnonymous]
        //public async Task<IActionResult> LoginCookie([FromBody] LoginDto request)
        //{
        //    var user = await _context.Users.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Username == request.Username);

        //    if (user == null || _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        //    {
        //        return Unauthorized("Invalid username or password");
        //    }

        //    await WriteToRedisAsync(user);

        //    var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        //    identity.AddClaim(ClaimTypes.NameIdentifier, user.Id.ToString()); // 注意：Cookie 模式常用 NameIdentifier
        //    identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString()); // 为了兼容 OpenIddict 的习惯
        //    identity.AddClaim(ClaimTypes.Name, user.Username);

        //    var principal = new ClaimsPrincipal(identity);

        //    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
        //    {
        //        IsPersistent = true, // "记住我"
        //        ExpiresUtc = DateTime.UtcNow.AddDays(7)
        //    });

        //    return Ok(new { Message = "Login successful via Cookie" });
        //}

        //[HttpPost("logout")] //logout AllDevice
        //[Authorize]
        //public async Task<IActionResult> Logout()
        //{
        //    var userId = _currentUserSession.UserId; // 从 Session 获取
        //    var cache = _redisProvider.GetRedisCache("User");
        //    await cache.RemoveAsync(userId.ToString());
        //    var subject = User.GetClaim(OpenIddictConstants.Claims.Subject);
        //    if (!string.IsNullOrEmpty(subject))
        //    {
        //         await _authorizationManager.RevokeBySubjectAsync(subject);
        //    }
        //    return Ok(new { Message = "Logged out successfully." });
        //}

        //[HttpPost("logout-cookie")]
        //public async Task<IActionResult> LogoutCookie()
        //{

        //    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        //    var userId = _currentUserSession.UserId;

        //    if (userId != Guid.Empty)
        //    {
        //        var cache = _redisProvider.GetRedisCache("User");
        //        await cache.RemoveAsync(userId.ToString());
        //    }

        //    return Ok(new { Message = "Logged out" });
        //}
        #endregion

        private IActionResult InvalidUserOrPassword()
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,

                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The username/password is invalid."
                }));
        }
    }
}