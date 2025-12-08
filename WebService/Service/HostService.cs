using Medallion.Threading;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Summary.Common.EFCore.DbContexts;
using Summary.Domain.Entities;
using Summary.Domain.Enums;
using Summary.Domain.Interfaces;

public class HostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDistributedLockProvider _lockProvider;

    public HostService(IServiceProvider serviceProvider, IDistributedLockProvider lockProvider)
    {
        _serviceProvider = serviceProvider;
        _lockProvider = lockProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // 1. 获取全局锁
        // "AppDbMigrationLock" 是自定义的锁名称，所有副本必须一致
        await using (var handle = await _lockProvider.AcquireLockAsync("AppDbMigrationLock", cancellationToken: cancellationToken))
        {

            using var scope = _serviceProvider.CreateScope();

            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<AppDbContext>>();
            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

            var adminSession = new AppTestSession();
            using var context = new AppDbContext(options, adminSession);

            // 只有第一个获得锁的进程会真正执行创建，
            // 后续获得锁的进程进来后，EnsureCreatedAsync 会发现表已存在并直接返回。
            await context.Database.EnsureCreatedAsync(cancellationToken);

            await SeedOpenIddictClientsAsync(manager, configuration, cancellationToken);

            await SeedBusinessDataAsync(context, passwordHasher, adminSession, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // 私有辅助方法：初始化 OpenIddict 客户端
    private async Task SeedOpenIddictClientsAsync(
    IOpenIddictApplicationManager manager,
    IConfiguration configuration,
    CancellationToken cancellationToken)
    {
        // 1. 机器专用客户端 (Machine-to-Machine)
        var machineDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "machine_client",
            ClientSecret = configuration["ApiKey"], // 必须有密钥
            DisplayName = "Background Service",
            ClientType = OpenIddictConstants.ClientTypes.Confidential, // 机密类型
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "api"
            }
        };
        await UpsertApplicationAsync(manager, machineDescriptor, cancellationToken);

        // 2. 用户/Swagger 专用客户端 (User Login)
        var userDescriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "swagger_user_client",
            ClientType = OpenIddictConstants.ClientTypes.Public, // Public 类型不需要 Secret (模拟手机/网页前端)
            DisplayName = "Swagger UI Test",

            // 如果用 Password 模式，其实不需要 RedirectUri
#if DEBUG
            RedirectUris = { new Uri("https://localhost:7072/swagger/oauth2-redirect.html") },
#else
            RedirectUris = { new Uri("your Uri") },
#endif

            Permissions =
            {
            OpenIddictConstants.Permissions.Endpoints.Token,
#if DEBUG
            // 允许 密码模式 (方便 Swagger 直接输账号密码)
            OpenIddictConstants.Permissions.GrantTypes.Password,
#endif
                // 允许 刷新令牌 (用户需要长效登录)
                OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

            // Scopes
            OpenIddictConstants.Permissions.Prefixes.Scope + "api",
            OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access",// 刷新令牌需要这个权限
            OpenIddictConstants.Permissions.Prefixes.Scope + "api.order"//特殊权限
            }
        };
        await UpsertApplicationAsync(manager, userDescriptor, cancellationToken);
    }

    // 核心通用逻辑：存在则更新，不存在则创建
    private async Task UpsertApplicationAsync(
        IOpenIddictApplicationManager manager,
        OpenIddictApplicationDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var existingApp = await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken);

        if (existingApp == null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            // 将 descriptor 的配置覆盖到 existingApp 上
            await manager.PopulateAsync(existingApp, descriptor, cancellationToken);
            // 保存更改
            await manager.UpdateAsync(existingApp, cancellationToken);
        }
    }

    private async Task SeedBusinessDataAsync(
        AppDbContext context,
        IPasswordHasher<User> passwordHasher,
        AppTestSession adminSession,
        CancellationToken cancellationToken)
    {
        // A. 初始化默认租户
        // 使用 Session 中的固定 ID，确保权限匹配
        var tenantId = adminSession.TenantId;
        var tenant = await context.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant == null)
        {
            tenant = new Tenant
            {
                Id = tenantId,
                Name = "Supper Tenant"
            };
            context.Tenants.Add(tenant);
            await context.SaveChangesAsync(cancellationToken);
        }

        // B. 初始化默认部门/组
        // 由于 Context 中有 AdminSession，Group.TenantId 会被 BaseDbContext 自动填充为 tenantId
        var rootGroup = await context.Groups.FirstOrDefaultAsync(g => g.TenantId == tenantId && g.ParentId == null, cancellationToken);
        if (rootGroup == null)
        {
            rootGroup = new Group
            {
                Id = Guid.NewGuid(),
                // TenantId = tenantId, // BaseDbContext 会自动处理
                Name = "Headquarters",
                ParentId = null
            };
            context.Groups.Add(rootGroup);
            await context.SaveChangesAsync(cancellationToken);
        }

        // C. 初始化管理员角色
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin" && r.TenantId == tenantId, cancellationToken);
        if (adminRole == null)
        {
            adminRole = new Role
            {
                Id = Guid.NewGuid(),
                // TenantId = tenantId, // BaseDbContext 会自动处理
                Name = "Admin"
            };

            adminRole.Permissions.Add(new RolePermission
            {
                RoleId = adminRole.Id,
                Resource = "All",
                Action = "All",
                Scope = DataScope.Tenant
            });

            context.Roles.Add(adminRole);
            await context.SaveChangesAsync(cancellationToken);
        }

        // D. 初始化管理员用户
        // 使用 Session 中的固定 ID
        var adminUserId = adminSession.UserId;
        var adminUser = await context.Users.FirstOrDefaultAsync(u => u.Id == adminUserId, cancellationToken);

        if (adminUser == null)
        {
            adminUser = new User
            {
                Id = adminUserId,
                // TenantId = tenantId, // BaseDbContext 会自动处理
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
}