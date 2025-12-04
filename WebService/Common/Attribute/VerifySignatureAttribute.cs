using AuthenticationServer.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AuthenticationServer.Common.Attribute
{
    [AttributeUsage(AttributeTargets.Method)]
    public class VerifySignatureAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var request = context.HttpContext.Request;
            var query = request.Query;

            // 1. 检查必要参数是否存在
            if (!query.TryGetValue("id", out var id) ||
                !query.TryGetValue("expires", out var expiresStr) ||
                !query.TryGetValue("sig", out var signature))
            {
                context.Result = new ContentResult
                {
                    StatusCode = 400,
                    Content = "Missing required parameters (id, expires, sig)"
                };
                return;
            }

            // 2. 解析过期时间
            if (!long.TryParse(expiresStr, out var expires))
            {
                context.Result = new ContentResult { StatusCode = 400, Content = "Invalid expiration format" };
                return;
            }

            var signer = context.HttpContext.RequestServices.GetRequiredService<IUrlSignerService>();

            // 4. 验证签名
            if (!signer.ValidateSignature(id.ToString(), expires, signature.ToString()))
            {
                context.Result = new ContentResult
                {
                    StatusCode = 403,
                    Content = "Invalid or expired signature link."
                };
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
