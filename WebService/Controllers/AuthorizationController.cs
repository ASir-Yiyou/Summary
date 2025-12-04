using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.Model;
using Summary.Common.Redis.Interfaces;
using Summary.Domain.Dtos;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace WebService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IRedisCacheProvider _redisProvider;
        private readonly IDbSession<Guid, Guid> _currentUserSession;

        public AuthController(
            AppDbContext context,
            IPasswordHasher<User> passwordHasher,
            IRedisCacheProvider redisProvider,
            IDbSession<Guid, Guid> currentUserSession)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _redisProvider = redisProvider;
            _currentUserSession = currentUserSession;
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
        [AllowAnonymous]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest();
            if (request == null) throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

            // --- A. 处理 "Password" 模式 (用户登录) ---
            if (request.IsPasswordGrantType())
            {
                // 1. 查库验证用户
                var user = await _context.Users
                    .AsNoTracking()
                    .IgnoreQueryFilters() // 登录前无 Session，必须忽略过滤器
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null) return InvalidUserOrPassword();

                // 2. 验证密码
                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
                if (result == PasswordVerificationResult.Failed) return InvalidUserOrPassword();

                var cache = _redisProvider.GetRedisCache("User");

                var sessionData = new UserSessionCache
                {
                    UserId = user.Id,
                    TenantId = user.TenantId,
                    //Roles = new List<string> { "User" }
                };

                // 缓存 Key: "session:{UserId}"
                // 过期时间: 2小时 (建议与 Token 有效期一致或略长)
                await cache.SetAsync(
                    user.Id.ToString(),
                    sessionData,
                    slidingExpireTime: TimeSpan.FromHours(2));

                // 4. 创建 Token 身份 (ClaimsIdentity)
                var identity = new ClaimsIdentity(
                    authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    nameType: OpenIddictConstants.Claims.Name,
                    roleType: OpenIddictConstants.Claims.Role);

                // 必须添加 Subject (UserId)，这是后续 Middleware 从 Token 找 Redis Key 的依据
                identity.AddClaim(OpenIddictConstants.Claims.Subject, user.Id.ToString());

                identity.AddClaim(OpenIddictConstants.Claims.Name, user.Username);

                // 5. 颁发 Token
                var principal = new ClaimsPrincipal(identity);

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }
            else if (request.IsClientCredentialsGrantType())
            {
                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId ?? "machine_client");
                identity.AddClaim(OpenIddictConstants.Claims.Name, request.ClientId ?? "machine_client");

                var principal = new ClaimsPrincipal(identity);
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
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

        private IActionResult InvalidUserOrPassword()
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    // OAuth 2.0 标准错误码：invalid_grant (通常用于账号密码错误)
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = OpenIddictConstants.Errors.InvalidGrant,

                    // 错误描述：给客户端看的具体信息
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The username/password is invalid."
                }));
        }
    }
}