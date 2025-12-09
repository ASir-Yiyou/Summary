using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Summary.Common.Core.Extensions
{
    public static class ServiceErrorExtensions
    {
        public static IApplicationBuilder UseServiceRedLogging(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ServiceErrorMiddleware>();
        }
    }

    public class ServiceErrorMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ServiceErrorMiddleware> _logger;

        public ServiceErrorMiddleware(RequestDelegate next, ILogger<ServiceErrorMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                LogRedError($"[Service Exception] {ex.GetType().Name}", context.Request, ex);
                throw;
            }
        }

        private void LogRedError(string title, HttpRequest request, Exception ex)
        {
            lock (Console.Out) // 防止日志错行
            {
                Console.WriteLine();
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"=== err {title} err ===");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" API: {request.Method} {request.Path}");
                Console.WriteLine($" Msg: {ex.Message}");

                // 简单打印堆栈前几行
                if (ex.StackTrace != null)
                {
                    var lines = ex.StackTrace.Split(Environment.NewLine).Take(3);
                    foreach (var line in lines) Console.WriteLine($"   {line.Trim()}");
                }

                Console.ResetColor();
                Console.WriteLine("=================================");
                Console.WriteLine();
            }
        }
    }
}
