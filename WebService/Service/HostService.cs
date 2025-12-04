using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Summary.Common.EFCore.DbContexts;
using Summary.Domain.Entities;
using Summary.Domain.Enums;
using Summary.Domain.Interfaces;

namespace WebService.Service
{
    public class HostService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public HostService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
            var adminSession = new AppTestSession();
            using var context = new AppDbContext(options, adminSession);
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            // 1. 确保数据库已创建
            // 注意：生产环境建议使用 context.Database.MigrateAsync()
            await context.Database.EnsureCreatedAsync(cancellationToken);

            if (await manager.FindByClientIdAsync("console_app_v1", cancellationToken) is null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "console_app_v1",
                    ClientSecret = configuration["ApiKey"],
                    DisplayName = "My Console App",
                    Permissions ={
                        OpenIddictConstants.Permissions.Endpoints.Token,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.GrantTypes.Password,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
                        OpenIddictConstants.Permissions.Prefixes.Scope + "api",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                        OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access"
                    }
                }, cancellationToken);
            }

            // 3. 初始化业务数据
            // 由于是在 HostedService 中运行，HttpContext 为空，Session.TenantId 为 null。
            // 根据你的 BaseTestDbContext 逻辑 (TenantId == null || ...)，这里可以查到所有数据，适合做初始化。

            // A. 初始化默认租户
            var tenant = await context.Tenants.FirstOrDefaultAsync(cancellationToken);
            if (tenant == null)
            {
                tenant = new Tenant
                {
                    Id = adminSession.TenantId,
                    Name = "Supper Tenant"
                };
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync(cancellationToken); // 先保存，为了生成 ID
            }

            // B. 初始化默认部门/组
            var rootGroup = await context.Groups.FirstOrDefaultAsync(g => g.TenantId == tenant.Id, cancellationToken);
            if (rootGroup == null)
            {
                rootGroup = new Group
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Headquarters",
                    ParentId = null
                };
                context.Groups.Add(rootGroup);
                await context.SaveChangesAsync(cancellationToken);
            }

            // C. 初始化管理员角色
            var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && r.TenantId == tenant.Id, cancellationToken);
            if (adminRole == null)
            {
                adminRole = new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Name = "Admin"
                };

                // 添加 "超级权限" (示例)
                adminRole.Permissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    Resource = "All",
                    Action = "All",
                    Scope = DataScope.Tenant // 最高级别：看整个租户
                });

                context.Roles.Add(adminRole);
                await context.SaveChangesAsync(cancellationToken);
            }

            var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Username == "admin", cancellationToken);
            if (adminUser == null)
            {
                adminUser = new User
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Username = "admin",
                    Email = "admin@example.com",
                    Address = "Server Room 1",
                    MainGroupId = rootGroup.Id,
                    SecurityStamp = Guid.NewGuid().ToString()
                };

                adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "123456");

                context.Users.Add(adminUser);

                context.Set<UserRole>().Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id
                });

                await context.SaveChangesAsync(cancellationToken);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}