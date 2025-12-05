using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Summary.Common.Core.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddAuthenticationServices(this IServiceCollection services, IConfiguration configuartion)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // 指向 Auth 服务获取公钥
                    options.Authority = configuartion["PublickeyAddress"] /*?? "https://localhost:7072"*/;
                    options.RequireHttpsMetadata = true;

                    //  开发环境：跳过 SSL 证书检查 (解决 The SSL connection could not be established)
#if DEBUG
                    options.BackchannelHttpHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    };

#endif

                    // 配置验证参数
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false, // 简化模式，不验证 Audience

                        // 开启颁发者验证
                        ValidateIssuer = true,
                        //  关键：同时信任 7072 (直连) 和 9000 (网关转发)
                        ValidIssuers = configuartion.GetSection("IssuerCollection").Get<string[]>() /*?? ["https://localhost:7072", "https://localhost:9000"]*/
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

        public static IServiceCollection AddSwaggerGenService(this IServiceCollection services, IConfiguration configuartion, OpenApiInfo info, string version = "v1")
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Product Service API", Version = "v1" });

                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Password = new OpenApiOAuthFlow
                        {
                            //  指向网关(9000)
                            TokenUrl = new Uri("https://localhost:9000/connect/token"),
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
}
