using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Summary.Common.EFCore.Extensions;
using Summary.Common.Redis.Extensions;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;
using WebService.Cache;
using WebService.Extensions;
using WebService.Service;

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

builder.Services.AddAppDbContext(builder.Configuration);

builder.Services.AddOpenIddictService();

builder.Services.AddSwaggerService();

builder.Services.AddHostedService<HostService>();

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
        options.OAuthClientId("console_app_v1");
        options.OAuthClientSecret(builder.Configuration["ApiKey"]);
        options.OAuthAppName("My App");
        options.OAuthUsePkce();
    });
}

app.MapControllers();

app.Run();