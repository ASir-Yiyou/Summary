using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Summary.Common.EFCore.Configurations;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.EFCore.Diagnostic;
using Summary.Domain.Interfaces;

namespace Summary.Common.EFCore.Interfaces
{
    public interface ITestDbContextFactory
    {
        MyTestDbContext CreateDbContext(string dbName);
    }

    public class MyTestDbContextFactory : ITestDbContextFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IDbSession<Guid, string> _session;
        private const string ConfigurationSectionName = "TestMultipleDbContextConfiguration";

        public MyTestDbContextFactory(IConfiguration configuration, IDbSession<Guid, string> session)
        {
            _configuration = configuration;
            _session = session;
        }

        public MyTestDbContext CreateDbContext(string dbName)
        {
            var dbConfigSection = _configuration.GetSection($"{ConfigurationSectionName}:{dbName}");

            if (!dbConfigSection.Exists())
            {
                throw new InvalidOperationException($"在配置的 '{ConfigurationSectionName}' 节下未找到名为 '{dbName}' 的数据库配置。");
            }

            var dbConfig = dbConfigSection.Get<TestDbContextConfiguration>();
            if (dbConfig == null || string.IsNullOrEmpty(dbConfig.ConnectionString))
            {
                throw new InvalidOperationException($"名为 '{dbName}' 的数据库配置无效或连接字符串为空。");
            }

            var optionsBuilder = new DbContextOptionsBuilder<MyTestDbContext>();

            var type = (DatabaseType)dbConfig.DbType;

            switch (type)
            {
                case DatabaseType.PSql:
                    optionsBuilder.UseNpgsql(dbConfig.ConnectionString);
                    break;
                //case DatabaseType.MSSQL:
                //    optionsBuilder.UseSqlServer(dbConfig.ConnectionString);
                //    break;
                //case DatabaseType.MSQL: // 假设 MSQL 是 MySql
                //                        // 你需要安装 Pomelo.EntityFrameworkCore.MySql 包
                //                        // optionsBuilder.UseMySql(dbConfig.ConnectionString, ServerVersion.AutoDetect(dbConfig.ConnectionString));
                //    throw new NotSupportedException("MySql 提供程序尚未配置，请先安装相关包并取消注释。");
                //case DatabaseType.MongoDb:
                //    throw new NotSupportedException("EF Core 不直接支持 MongoDB。");
                default:
                    throw new NotSupportedException($"不支持的数据库类型: {dbConfig.DbType}");
            }

            return new MyTestDbContext(optionsBuilder.Options, _session);
        }
    }
}