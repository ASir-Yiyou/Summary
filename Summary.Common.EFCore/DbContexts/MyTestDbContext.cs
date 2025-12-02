using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace Summary.Common.EFCore.DbContexts
{
    //dotnet ef migrations add InitialCreate 迁移脚本
    //dotnet ef database update 生成表格
    public class MyTestDbContext : BaseTestDbContext<Guid, string>
    {
        public MyTestDbContext(DbContextOptions dbContextOptions,
            ITestSession<Guid, string> session) : base(dbContextOptions, session)
        {
        }

        public DbSet<Bill> Bills { get; set; }
        public DbSet<Custom> Customs { get; set; }
        public DbSet<TimeLine> TimeLines { get; set; }
        public DbSet<Milestone> Milestones { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Bill>(entity =>
            {
                entity.Property(e => e.Amount)
                      .HasPrecision(18, 2);
            });
            modelBuilder.Entity<Custom>().ToTable("Customers");
        }
    }

    #region 快速实现

    public class MyTestDbContextFactory : IDesignTimeDbContextFactory<MyTestDbContext>
    {
        public MyTestDbContextFactory()
        {
        }

        public MyTestDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<MyTestDbContext>();
            optionsBuilder.UseNpgsql("Host=localhost;Database=postgres;User ID=postgres;Password=1023");
            return new MyTestDbContext(optionsBuilder.Options, new TestSession());
        }
    }

    #endregion 快速实现

    #region 配置创建

    //public class MyTestDbContextFactory : IDesignTimeDbContextFactory<MyTestDbContext>
    //{
    //    private const string ConfigurationSectionName = "TestMultipleDbContextConfiguration";

    //    public MyTestDbContext CreateDbContext(string[] args)
    //    {
    //        IConfigurationRoot configuration = new ConfigurationBuilder()
    //            .SetBasePath(Directory.GetCurrentDirectory())
    //            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    //            .AddEnvironmentVariables()
    //            .Build();

    //        string dbName = nameof(MyTestDbContext);
    //        if (args.Length != 0 && string.IsNullOrEmpty(args[0]))
    //        {
    //            dbName = args[0];
    //        }

    //        var dbConfigSection = configuration.GetSection($"{ConfigurationSectionName}:{dbName}");

    //        if (!dbConfigSection.Exists())
    //        {
    //            try
    //            {
    //                dbName = dbName[..^7];
    //                dbConfigSection = configuration.GetSection($"{ConfigurationSectionName}:{dbName}");
    //            }
    //            catch (Exception) { }
    //            if (!dbConfigSection.Exists()) throw new InvalidOperationException($"在配置的 '{ConfigurationSectionName}' 节下未找到名为 '{dbName}' 的数据库配置。");
    //        }

    //        var dbConfig = dbConfigSection.Get<TestDbContextConfiguration>();
    //        if (dbConfig == null || string.IsNullOrEmpty(dbConfig.ConnectionString))
    //        {
    //            throw new InvalidOperationException($"名为 '{dbName}' 的数据库配置无效或连接字符串为空。");
    //        }

    //        var optionsBuilder = new DbContextOptionsBuilder<MyTestDbContext>();

    //        var type = (DatabaseType)dbConfig.DbType;

    //        switch (type)
    //        {
    //            case DatabaseType.PSql:
    //                optionsBuilder.UseNpgsql(dbConfig.ConnectionString);
    //                break;
    //            //case DatabaseType.MSSQL:
    //            //    optionsBuilder.UseSqlServer(dbConfig.ConnectionString);
    //            //    break;
    //            //case DatabaseType.MSQL: // 假设 MSQL 是 MySql
    //            //                        // 你需要安装 Pomelo.EntityFrameworkCore.MySql 包
    //            //                        // optionsBuilder.UseMySql(dbConfig.ConnectionString, ServerVersion.AutoDetect(dbConfig.ConnectionString));
    //            //    throw new NotSupportedException("MySql 提供程序尚未配置，请先安装相关包并取消注释。");
    //            //case DatabaseType.MongoDb:
    //            //    throw new NotSupportedException("EF Core 不直接支持 MongoDB。");
    //            default:
    //                throw new NotSupportedException($"不支持的数据库类型: {dbConfig.DbType}");
    //        }

    //        return new MyTestDbContext(optionsBuilder.Options, new TestSession());
    //    }
    //}

    #endregion 配置创建
}