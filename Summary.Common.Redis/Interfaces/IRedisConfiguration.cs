using System.Collections.Generic;

namespace Summary.Common.Redis.Interfaces
{
    public interface IRedisConfiguration
    {
        string RedisConnection { get; set; }

        string CacheName { get; set; }

        int DefaultDB { get; set; }

        int IdleTimeout { get; set; }
    }

    public interface IMultipleRedisConfiguration
    {
        ICollection<IRedisConfiguration> Configurations { get; }
    }
}
