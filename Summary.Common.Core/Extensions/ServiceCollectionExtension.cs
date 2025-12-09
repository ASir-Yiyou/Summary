using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // https://auth (Docker内部/Aspire内部通信)
        var authInternalUrl = configuration["Identity:AuthInternalUrl"] ?? "https://auth";
        // https://localhost:9000 (浏览器可见的网关地址)
        var gatewayPublicUrl = configuration["Identity:GatewayPublicUrl"] ?? "https://localhost:9000";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // [后端通信] 指向 Auth 服务获取公钥
                // Aspire 会自动把 "https://auth" 解析为对应的 IP
                options.Authority = authInternalUrl;

                options.RequireHttpsMetadata = true;
                options.Audience = "api"; // 建议加上 Audience 验证，更安全

                // 开发环境：跳过 SSL 证书检查
#if DEBUG
                options.BackchannelHttpHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
#endif

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = false, // 如果需要简化，可以保持 false
                    ValidateIssuer = true,

                    // [关键修改] 动态验证发行者
                    // Token 可能是由 auth 服务直接签发的 (iss: https://auth)
                    // 也可能是通过网关转发签发的 (iss: https://localhost:9000)
                    // 我们把这两个都加进白名单
                    ValidIssuers = new[]
                    {
                        authInternalUrl,
                        gatewayPublicUrl,
                        "https://localhost:7072" // 兼容旧的本地调试地址
                    }
                };

                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[Auth Failed]: {context.Exception.Message}");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = context =>
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[Auth Success]: User {context.Principal?.Identity?.Name}");
                        Console.ResetColor();
                        return Task.CompletedTask;
                    }
                };
            });
        return services;
    }

    public static IServiceCollection AddSwaggerGenService(this IServiceCollection services, IConfiguration configuration, OpenApiInfo info, string version = "v1")
    {
        // 获取外部网关地址 (因为 Swagger 是跑在浏览器里的)
        var gatewayPublicUrl = configuration["Identity:GatewayPublicUrl"] ?? "https://localhost:9000";

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo { Title = info.Title, Version = version });

            c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.OAuth2,
                Flows = new OpenApiOAuthFlows
                {
                    Password = new OpenApiOAuthFlow
                    {
                        // [前端通信] 指向网关的公网地址
                        // 注意：这里绝对不能写 https://auth 或 https://gateway，因为浏览器不认识这两个名字
                        TokenUrl = new Uri($"{gatewayPublicUrl}/connect/token"),

                        Scopes = new Dictionary<string, string>
                        {
                            { "api", "Access API" },
                            { "offline_access", "Refresh Token" }
                        }
                    }
                }
            });

            c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}