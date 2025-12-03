using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using Summary.Common.EFCore.DbContexts;

namespace WebService.Extensions
{
    public static class IServiceExtension
    {
        public static IServiceCollection AddOpenIddictService(this IServiceCollection services)
        {

            services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore().UseDbContext<AppDbContext>();
                })
                .AddServer(options =>
                {
                    options.SetTokenEndpointUris("/connect/token");
                    options.AllowClientCredentialsFlow();
                    options.AllowPasswordFlow();
                    options.AddDevelopmentEncryptionCertificate().AddDevelopmentSigningCertificate();
                    options.UseAspNetCore().EnableTokenEndpointPassthrough();
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                });

            services.AddAuthentication(options =>
            {
                // 设置默认方案为 OpenIddict 的验证方案
                options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
            return services;
        }

        public static IServiceCollection AddSwaggerService(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Description = "OAuth2.0 Auth Code with PKCE",
                    Flows = new OpenApiOAuthFlows
                    {
                        // 1. 机器模式
                        ClientCredentials = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri("/connect/token", UriKind.Relative),
                            Scopes = new Dictionary<string, string> { { "api", "Machine Access" } }
                        },
                        // 2. 用户密码模式 (Swagger UI 会弹出用户名/密码框)
                        Password = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri("/connect/token", UriKind.Relative),
                            Scopes = new Dictionary<string, string> { { "api", "User Access" } }
                        }
                    }
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
