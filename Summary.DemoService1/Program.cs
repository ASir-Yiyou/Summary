using Microsoft.OpenApi.Models;
using Summary.Common.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
#if DEBUG
Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
#endif

builder.Services.AddHealthChecks();//注册健康检查服务

// 1. 配置 JWT 鉴权
builder.Services.AddAuthenticationServices(builder.Configuration);

// 2. 配置 Swagger
builder.Services.AddSwaggerGenService(builder.Configuration, new OpenApiInfo { Title = "Order Service API", Version = "v1" });

// 3. 路由设置
builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true;
});

var app = builder.Build();

app.UseHttpsRedirection();

// 4. 中间件管道
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");

app.MapControllers();

app.Run();