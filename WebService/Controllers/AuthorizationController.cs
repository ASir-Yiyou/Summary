using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace WebService.Controllers
{
    public class AuthorizationController : Controller
    {
        [HttpPost("~/connect/token")]
        public async Task<IActionResult> Exchange()
        {
            // 1. 解析 OpenIddict 请求
            var request = HttpContext.GetOpenIddictServerRequest();

            // 2. 处理 "Client Credentials" 模式
            if (request.IsClientCredentialsGrantType())
            {
                // 注意：OpenIddict 已经自动校验了 ClientId 和 ClientSecret，
                // 能进到这里说明客户端身份是合法的。

                // 3. 创建用户身份 (ClaimsPrincipal)
                var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

                // 添加 Subject (必须，通常是 ClientId)
                identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId ?? "unknown");
                identity.AddClaim(OpenIddictConstants.Claims.Name, "My Console Application");

                var principal = new ClaimsPrincipal(identity);

                // 设置 Scopes (可选)
                principal.SetScopes(request.GetScopes());

                // 4. 颁发 Token
                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new NotImplementedException("The specified grant type is not implemented.");
        }
    }
}
