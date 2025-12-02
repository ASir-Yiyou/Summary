using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using WebService.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. 配置 EF Core 内存数据库 (生产环境请换成 SQL Server 等)
builder.Services.AddDbContext<DbContext>(options =>
{
    options.UseInMemoryDatabase(nameof(DbContext));
    options.UseOpenIddict(); // 注册 OpenIddict 的实体
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("/connect/token", UriKind.Relative),
                Scopes = new Dictionary<string, string>
                {
                    { "api", "Access to API" } // 如果你的 Demo 没用到 Scope，这里其实可以是空的
                }
            }
        }
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new string[] {}
        }
    });
});

// 2. 配置 OpenIddict
builder.Services.AddOpenIddict()
    // 2.1 配置核心组件 (使用 EF Core)
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<DbContext>();
    })
    // 2.2 配置服务端 (Server)
    .AddServer(options =>
    {
        // 启用 Token 端点 (用于获取 Token)
        options.SetTokenEndpointUris("/connect/token");

        // 允许 "Client Credentials" 流程 (机器对机器)
        options.AllowClientCredentialsFlow();

        // 注册签名和加密证书 (开发环境使用临时证书)
        // **重要**：生产环境必须使用真实的 X.509 证书！
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // 注册 ASP.NET Core 主机并禁用 HTTPS 传输要求 (仅限开发环境!)
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough();
    })
    // 2.3 配置验证 (用于在这个 API 本身验证 Token)
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddControllers();
builder.Services.AddHostedService<TestData>(); // 用于初始化数据

var app = builder.Build();

app.UseRouting();

// 3. 启用鉴权与授权
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    // 2. 配置 Swagger UI (UseSwaggerUI)
    app.UseSwaggerUI(options =>
    {
        // 告诉 Swagger UI 使用 OAuth2
        options.OAuthClientId("console_app"); // 对应数据库里的 ClientId
        options.OAuthClientSecret("my-secret-key"); // 对应数据库里的 ClientSecret
        options.OAuthAppName("My Console App");
    });
}

app.MapControllers();

app.Run();