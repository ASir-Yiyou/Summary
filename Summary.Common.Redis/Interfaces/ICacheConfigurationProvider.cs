using System;

namespace Summary.Common.Redis.Interfaces
{
    public interface ICacheConfigurationProvider
    {
        void Configure(string cacheName, Action<IRedisCacheOption> action);

        void ConfigureAll(Action<IRedisCacheOption> action);

        void ApplyConfiguration(string cacheName, IRedisCacheOption target);
    }

    public interface ICacheConfiguration
    {
        string CacheName { get; }

        Action<IRedisCacheOption>? Action { get; }
    }
}