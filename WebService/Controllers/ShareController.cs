using AuthenticationServer.Common.Attribute;
using AuthenticationServer.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuthenticationServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShareController : ControllerBase
    {
        private readonly IUrlSignerService _urlSigner;

        public ShareController(IUrlSignerService urlSigner)
        {
            _urlSigner = urlSigner;
        }


        [HttpPost("generate")]
        [Authorize]
        public IActionResult GenerateLink([FromBody] string orderId)
        {
            var url = _urlSigner.GenerateSignedUrl(orderId, TimeSpan.FromHours(24));
            return Ok(new { ShareUrl = url });
        }

        [HttpGet("view")]
        [VerifySignature]
        public IActionResult ViewSharedOrder(string id, long expires, string sig)
        {
            return Ok(new
            {
                Message = "Success! You are viewing a protected shared resource.",
                ResourceId = id,
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(expires)
            });
        }
    }
}
