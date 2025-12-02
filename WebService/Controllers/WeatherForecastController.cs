using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;
using Summary.Common.Redis.Interfaces;

namespace WebService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    // 指定使用 OpenIddict 的验证方案
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public class WeatherController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // 如果能进到这里，说明 Token 有效
            return Ok($"Hello! Your Client ID is: {User.FindFirst(OpenIddict.Abstractions.OpenIddictConstants.Claims.Subject)?.Value}");
        }
    }
}
