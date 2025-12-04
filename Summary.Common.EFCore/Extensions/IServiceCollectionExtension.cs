using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Summary.Common.EFCore.Configurations;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.EFCore.Diagnostic;

namespace Summary.Common.EFCore.Extensions
{
    public static class IServiceCollectionExtension
    {
        private const string ConfigurationSectionName = "AppMultipleDbContextConfiguration";
        private const string DefaultDbName = "AppDbContext";

        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var dbConfigSection = configuration.GetSection($"{ConfigurationSectionName}:{DefaultDbName}");

            if (!dbConfigSection.Exists())
            {
                var shortName = DefaultDbName.Replace("DbContext", "");
                var retrySection = configuration.GetSection($"{ConfigurationSectionName}:{shortName}");
                if (retrySection.Exists())
                {
                    dbConfigSection = retrySection;
                }
                else
                {
                    throw new InvalidOperationException($"未在配置中找到 '{ConfigurationSectionName}:{DefaultDbName}' 或其变体。");
                }
            }

            var dbConfig = dbConfigSection.Get<TestDbContextConfiguration>();
            if (dbConfig == null || string.IsNullOrEmpty(dbConfig.ConnectionString))
            {
                throw new InvalidOperationException($"数据库配置无效或连接字符串为空。");
            }

            // 2. 注册 DbContext
            services.AddDbContext<AppDbContext>(options =>
            {
                switch ((DatabaseType)dbConfig.DbType)
                {
                    case DatabaseType.PSql:
                        options.UseNpgsql(dbConfig.ConnectionString);
                        break;

                    default:
                        throw new NotSupportedException($"不支持的数据库类型 ID: {dbConfig.DbType}");
                }
            });

            return services;
        }
    }
}