using AuthenticationServer.Cache;
using AuthenticationServer.Extensions;
using AuthenticationServer.Service;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using Summary.Common.EFCore.Extensions;
using Summary.Common.Redis.Extensions;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// --- 1. 基础服务注册 ---
builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();

builder.Services.AddRedis(builder.Configuration, "RedisConfig", policy =>
{
    policy.Configure("User", opt => opt.DefaultSlidingExpireTime = TimeSpan.FromHours(2));
});
builder.Services.AddScoped<RequestScopedSession>();

builder.Services.AddScoped<IDbSession<Guid, Guid>>(sp => sp.GetRequiredService<RequestScopedSession>());

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddSingleton<IUrlSignerService, UrlSignerService>();

builder.Services.AddAppDbContext(builder.Configuration);

builder.Services.AddOpenIddictService();

builder.Services.AddSwaggerService();

builder.Services.AddHostedService<HostService>();

builder.Services.AddHostedService<MyBackgroundService>();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<RedisSessionMiddleware>();

// 3. 后授权
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.OAuthClientId("swagger_user_client");
        options.OAuthAppName("Swagger UI");
        options.OAuthScopes("api", "offline_access");
        options.OAuthUsePkce();
    });
}

app.MapControllers();

app.Run();