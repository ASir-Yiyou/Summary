using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Summary.Common.EFCore.Configurations;
using Summary.Common.EFCore.Diagnostic;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace Summary.Common.EFCore.DbContexts
{
    public class AppDbContext : BaseTestDbContext<Guid, Guid>
    {
        private readonly IDbSession<Guid, Guid> _currentUser;

        public AppDbContext(DbContextOptions dbContextOptions,
            IDbSession<Guid, Guid> session) : base(dbContextOptions, session)
        {
            _currentUser = session;
        }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.UseOpenIddict();

            modelBuilder.Entity<Group>()
                .HasOne(g => g.Parent)
                .WithMany(g => g.Children)
                .HasForeignKey(g => g.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.Resource, rp.Action });
        }
    }

    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        private const string ConfigurationSectionName = "AppMultipleDbContextConfiguration";

        public AppDbContext CreateDbContext(string[] args)
        {
            string basePath = GetConfigurationBasePath();
            Console.WriteLine($"正在从以下路径读取配置: {basePath}");

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            string dbName = nameof(AppDbContext);

            if (args != null && args.Length > 0 && !string.IsNullOrEmpty(args[0]))
            {
                dbName = args[0];
            }

            var dbConfigSection = configuration.GetSection($"{ConfigurationSectionName}:{dbName}");

            if (!dbConfigSection.Exists())
            {
                if (dbName.EndsWith("DbContext"))
                {
                    var shortName = dbName.Replace("DbContext", "");
                    var retrySection = configuration.GetSection($"{ConfigurationSectionName}:{shortName}");
                    if (retrySection.Exists())
                    {
                        dbConfigSection = retrySection;
                        dbName = shortName;
                    }
                }

                if (!dbConfigSection.Exists())
                    throw new InvalidOperationException($"在 '{basePath}\\appsettings.json' 的 '{ConfigurationSectionName}' 节下未找到名为 '{dbName}' 或其变体的配置。");
            }

            var dbConfig = dbConfigSection.Get<TestDbContextConfiguration>();

            if (dbConfig == null || string.IsNullOrEmpty(dbConfig.ConnectionString))
            {
                throw new InvalidOperationException($"名为 '{dbName}' 的数据库配置无效或连接字符串为空。");
            }

            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var type = (DatabaseType)dbConfig.DbType;

            switch (type)
            {
                case DatabaseType.PSql:
                    optionsBuilder.UseNpgsql(dbConfig.ConnectionString);
                    break;
                // case DatabaseType.MSSQL:
                //     optionsBuilder.UseSqlServer(dbConfig.ConnectionString);
                //     break;
                default:
                    throw new NotSupportedException($"不支持的数据库类型 ID: {dbConfig.DbType}");
            }

            IDbSession<Guid, Guid> session = new AppTestSession();
            return new AppDbContext(optionsBuilder.Options, session);
        }

        private string GetConfigurationBasePath()
        {
            var currentDir = Directory.GetCurrentDirectory();

            if (File.Exists(Path.Combine(currentDir, "appsettings.json")))
            {
                return currentDir;
            }

            var dirInfo = new DirectoryInfo(currentDir);
            while (dirInfo != null)
            {
                var targetPath = Path.Combine(dirInfo.FullName, "WebService");
                if (Directory.Exists(targetPath) && File.Exists(Path.Combine(targetPath, "appsettings.json")))
                {
                    return targetPath;
                }
                dirInfo = dirInfo.Parent;
            }

            string hardcodedPath = @"D:\Code\Demo\Summary\WebService";
            if (Directory.Exists(hardcodedPath))
            {
                return hardcodedPath;
            }

            throw new DirectoryNotFoundException($"无法找到配置文件。已搜索路径 '{currentDir}' 及其上级，且默认路径 '{hardcodedPath}' 不存在。");
        }
    }
}