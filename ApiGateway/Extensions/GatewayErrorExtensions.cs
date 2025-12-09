using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

public static class GatewayErrorExtensions
{
    public static IApplicationBuilder UseGatewayRedLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GatewayErrorMiddleware>();
    }
}

public class GatewayErrorMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewayErrorMiddleware> _logger;

    public GatewayErrorMiddleware(RequestDelegate next, ILogger<GatewayErrorMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. 保留原始的响应流 (以便最后发回给用户)
        var originalBodyStream = context.Response.Body;

        // 2. 创建一个临时的内存流
        using var memoryStream = new MemoryStream();
        // 3. 偷梁换柱：告诉后续的中间件(YARP)，把响应写到我的内存流里
        context.Response.Body = memoryStream;

        try
        {
            // 4. 执行 YARP 转发
            await _next(context);

            // 场景 A: YARP 自身报错 (如 502 Bad Gateway / 连接拒绝)
            var forwarderError = context.Features.Get<IForwarderErrorFeature>();
            if (forwarderError != null)
            {
                LogRedError(
                    $"[YARP Connection Error] {forwarderError.Error}",
                    context.Request,
                    context.Response.StatusCode,
                    forwarderError.Exception?.Message ?? "Connection failed"
                );
                // 此时通常没有 Body，直接返回
                memoryStream.Seek(0, SeekOrigin.Begin);
                await memoryStream.CopyToAsync(originalBodyStream);
                return;
            }

            // 场景 B: 转发成功，但下游返回了 404/500 等业务错误
            if (context.Response.StatusCode >= 400)
            {
                // 读取响应内容 (详细信息就在这里！)
                memoryStream.Seek(0, SeekOrigin.Begin);
                var responseBodyText = await new StreamReader(memoryStream).ReadToEndAsync();

                // 重置指针，准备后续复制
                memoryStream.Seek(0, SeekOrigin.Begin);

                // 如果 Body 为空，给个提示
                if (string.IsNullOrWhiteSpace(responseBodyText))
                {
                    responseBodyText = "(Response body is empty)";
                }

                LogRedError(
                    $"[Downstream HTTP Error] {context.Response.StatusCode}",
                    context.Request,
                    context.Response.StatusCode,
                    responseBodyText
                );
            }

            // 5. 将内存流的内容复制回原始响应流，发送给客户端
            memoryStream.Seek(0, SeekOrigin.Begin);
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            // 恢复原始流，防止这里崩了导致客户端收不到任何东西
            context.Response.Body = originalBodyStream;
            LogRedError($"[Gateway Exception] {ex.GetType().Name}", context.Request, 500, ex.Message);
            throw;
        }
    }

    private void LogRedError(string title, HttpRequest request, int statusCode, string details)
    {
        lock (Console.Out)
        {
            Console.WriteLine();

            // 颜色策略
            if (statusCode >= 500) Console.BackgroundColor = ConsoleColor.DarkRed;
            else Console.BackgroundColor = ConsoleColor.DarkMagenta; // 404 用紫色

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"===  {title}  ===");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" URL: {request.Method} {request.Path}{request.QueryString}");
            Console.WriteLine($" Status: {statusCode}");

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($" Details: {details}");

            Console.ResetColor();
            Console.WriteLine("=================================");
            Console.WriteLine();
        }
    }
}