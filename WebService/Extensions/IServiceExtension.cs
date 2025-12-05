using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Summary.Common.EFCore.DbContexts;

namespace AuthenticationServer.Extensions
{
    public static class IServiceExtension
    {
        public static IServiceCollection AddOpenIddictService(this IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                // 设置默认方案为 OpenIddict 的验证方案
                options.DefaultScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            });
            #region Cookie
            //.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            // {
            //     // 配置 Cookie 属性
            //     options.Cookie.Name = "MyAuthCookie";
            //     options.Cookie.HttpOnly = true; //禁止 JS 读取，防 XSS
            //     options.Cookie.SameSite = SameSiteMode.Strict; // 防 CSRF
            //     options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // 仅限 HTTPS

            //     options.ExpireTimeSpan = TimeSpan.FromDays(7);

            //     options.Events.OnRedirectToLogin = context =>
            //     {
            //         context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            //         return Task.CompletedTask;
            //     };
            // }).AddPolicyScheme(AuthScheme, "Bearer or Cookie", options =>
            // {
            //     options.ForwardDefaultSelector = context =>
            //     {
            //         var authHeader = context.Request.Headers["Authorization"].ToString();

            //         if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            //         {
            //             return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
            //         }

            //         return CookieAuthenticationDefaults.AuthenticationScheme;
            //     };
            // });
            #endregion

            services.AddOpenIddict()
                .AddCore(options =>
                {
                    options.UseEntityFrameworkCore().UseDbContext<AppDbContext>();
                })
                .AddServer(options =>
                {
                    options.SetTokenEndpointUris("/connect/token");
                    // 允许机器模式
                    options.AllowClientCredentialsFlow();
                    // 允许密码模式 (给测试用)
                    // 密钥分享给其他程序
                    options.DisableAccessTokenEncryption();
#if DEBUG
                    options.AllowPasswordFlow();
#endif
                    // 允许刷新令牌
                    options.AllowRefreshTokenFlow();
                    options.RegisterScopes("api", "api.order", "offline_access");
                    options.UseAspNetCore().EnableTokenEndpointPassthrough(); //不开启可能导致OpenIddict 可能会在请求到达 Controller 之前就拦截它，或者虽然放行了但没有解析 OIDC 参数
#if DEBUG
                    options.AddDevelopmentEncryptionCertificate()
                               .AddDevelopmentSigningCertificate();
#else
                    var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2("pfx file", "your password");

                    //string keyString = Configuration["OpenIddict:Key"] ?? "3100f35b137dfe1e1e41512f0a397f19-aaf4d572718d248f66a189a9a1007486";
                    //var securityKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey( System.Text.Encoding.UTF8.GetBytes(keyString));
                    //options.AddSigningKey(securityKey);
                    //options.AddEncryptionKey(securityKey);

                    options.AddSigningCertificate(cert);
                    options.AddEncryptionCertificate(cert);
#endif
                })
                .AddValidation(options =>
                {
                    options.UseLocalServer();
                    options.UseAspNetCore();
                    //options.EnableTokenEntryValidation();//强制每次 API 请求都去数据库检查 Token ID 是否有效/是否被撤销，一般不开
                });

            services.AddAuthorizationBuilder().AddPolicy("OrderAccessPolicy", policy =>
            {
                policy.RequireAssertion(context =>
                {
                    var scopeClaims = context.User.FindAll(OpenIddictConstants.Claims.Scope).Select(c => c.Value);
                    //有坑，它把同一个 Claim Type 的多个值合并成一个字符串，用空格分隔了
                    return scopeClaims.Any(s => s.Split(' ').Contains("api.order"));
                });
            });

            services.AddRateLimiter(options =>
            {
                // 对登录接口特殊限制：每分钟只能试 5 次
                options.AddFixedWindowLimiter("LoginPolicy", opt =>
                {
                    opt.PermitLimit = 5;
                    opt.Window = TimeSpan.FromMinutes(1);
                    opt.QueueLimit = 0; // 超过直接拒
                });
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
                    Flows = new OpenApiOAuthFlows
                    {
                        //// 方式 A: 机器登录 (Client Credentials)
                        //ClientCredentials = new OpenApiOAuthFlow
                        //{
                        //    TokenUrl = new Uri("/connect/token", UriKind.Relative),
                        //    Scopes = new Dictionary<string, string> {{ "api", "Machine Access" } } // 机器模式这里不要加 offline_access
                        //},
                        // 方式 B: 用户登录 (Password)
                        Password = new OpenApiOAuthFlow
                        {
                            TokenUrl = new Uri("/connect/token", UriKind.Relative),
                            Scopes = new Dictionary<string, string>
                            {
                                { "api", "Base Access" },
                                { "offline_access", "Get Refresh Token" },
                                { "api.order", "Manage Orders" }
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
                        new string[] { }// 允许的 Scopes，留空表示所有
                    }
                });
            });
            return services;
        }
    }
}