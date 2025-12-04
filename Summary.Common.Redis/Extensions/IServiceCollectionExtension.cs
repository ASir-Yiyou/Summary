using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Summary.Common.Redis.Impls;
using Summary.Common.Redis.Interfaces;

namespace Summary.Common.Redis.Extensions
{
    public static class IServiceCollectionExtension
    {
        public static IServiceCollection AddRedis(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "RedisConfig",
        Action<ICacheConfigurationProvider>? provider = null)
        {
            var configs = new List<RedisConfiguration>();
            configuration.GetSection(sectionName).Bind(configs);

            if (configs.Count == 0)
            {
                throw new Exception("Unable to find Redis configuration");
            }

            var multiConfig = new MultipleRedisConfiguration
            {
                Configurations = [.. configs]
            };
            services.AddSingleton<IMultipleRedisConfiguration>(multiConfig);

            var configProvider = new CacheConfigurationProvider();
            provider?.Invoke(configProvider);
            services.AddSingleton<ICacheConfigurationProvider>(configProvider);

            services.AddSingleton<IRedisCacheProvider, RedisCacheProvider>();

            return services;
        }
    }
}