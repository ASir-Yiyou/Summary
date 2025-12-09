using AuthenticationServer.Cache;
using AuthenticationServer.Extensions;
using AuthenticationServer.Service;
using Medallion.Threading;
using Medallion.Threading.Postgres;
using Microsoft.AspNetCore.Identity;
using Summary.Common.Core.Extensions;
using Summary.Common.EFCore.Extensions;
using Summary.Common.Redis.Extensions;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// --- 1. 基础服务注册 ---
builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor();

builder.Services.AddHealthChecks();//注册健康检查服务

builder.Services.AddRedis(builder.Configuration, "RedisConfig", policy =>
{
    policy.Configure("User", opt => opt.DefaultSlidingExpireTime = TimeSpan.FromHours(2));
});
builder.Services.AddScoped<RequestScopedSession>();

builder.Services.AddScoped<IDbSession<Guid, Guid>>(sp => sp.GetRequiredService<RequestScopedSession>());

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddSingleton<IUrlSignerService, UrlSignerService>();

builder.Services.AddAppDbContext(builder.Configuration);

//2. 注册 PostgreSQL 分布式锁
builder.Services.AddSingleton<IDistributedLockProvider>(sp => 
{
    var connectionString = builder.Configuration.GetSection("AppMultipleDbContextConfiguration:AppDbContext:ConnectionString").Value;
    return new PostgresDistributedSynchronizationProvider(connectionString);
});

builder.Services.AddOpenIddictService();

builder.Services.AddSwaggerService();

builder.Services.AddHostedService<HostService>();

builder.Services.AddHostedService<MyBackgroundService>();

builder.Services.AddRouting(options =>
{
    options.LowercaseUrls = true;
    options.LowercaseQueryStrings = true; // 可选：查询参数也小写
});

var app = builder.Build();

app.UseRouting();
app.UseServiceRedLogging();

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

app.MapHealthChecks("/health");//映射健康检查接口

app.MapControllers();

app.Run();