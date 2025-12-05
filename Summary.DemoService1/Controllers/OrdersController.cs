using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Summary.DemoService1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get()
        {
            // 获取 Token 中的用户 ID
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            return Ok(new { Service = "Order Service", OrderId = 12345, User = userId });
        }
    }
}
