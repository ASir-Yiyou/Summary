using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Summary.Common.Redis.Interfaces;

namespace Summary.Common.Redis.Impls
{
    public class RedisCacheConfiguration : ICacheConfiguration
    {
        public RedisCacheConfiguration(string cacheName, [NotNull] Action<IRedisCacheOption> action)
        {
            CacheName = cacheName;
            Action = action;
        }

        public string CacheName { get; set; }

        public Action<IRedisCacheOption> Action { get; set; }
    }

    public class RedisConfiguration : IRedisConfiguration
    {
        public string RedisConnection { get; set; } = "localhost:6379";
        public string CacheName { get; set; } = "Default";
        public int DefaultDB { get; set; } = 0;
        public int IdleTimeout { get; set; } = 5000;
    }

    public class MultipleRedisConfiguration : IMultipleRedisConfiguration
    {
        public ICollection<IRedisConfiguration> Configurations { get; set; } = new List<IRedisConfiguration>();
    }
}