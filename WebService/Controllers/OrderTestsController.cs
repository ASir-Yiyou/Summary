using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // 只有持有 api.order scope 的 Token 才能访问这个类里的所有方法
    [Authorize(Policy = "OrderAccessPolicy")]
    public class OrderTestsController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetOrders()
        {
            return Ok(new { Message = "You have access to orders!" });
        }

        [HttpPost]
        public IActionResult CreateOrder()
        {
            return Ok(new { Message = "Order created" });
        }
    }
}
