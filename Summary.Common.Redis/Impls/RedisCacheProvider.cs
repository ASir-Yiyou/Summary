using System;
using System.Collections.Concurrent;
using System.Linq;
using StackExchange.Redis;
using Summary.Common.Redis.Interfaces;

namespace Summary.Common.Redis.Impls
{
    public class RedisCacheProvider : IRedisCacheProvider, IDisposable
    {
        private readonly IMultipleRedisConfiguration _multiConfig;
        private readonly ICacheConfigurationProvider _cachePolicyProvider;

        private readonly ConcurrentDictionary<string, Lazy<ConnectionMultiplexer>> _connections = new();
        private readonly ConcurrentDictionary<string, IRedisCache> _caches = new();

        public RedisCacheProvider(IMultipleRedisConfiguration multiConfig, ICacheConfigurationProvider cachePolicyProvider)
        {
            _multiConfig = multiConfig;
            _cachePolicyProvider = cachePolicyProvider;
        }

        public IRedisCache GetRedisCache(string cachename)
        {
            if (_caches.TryGetValue(cachename, out var existingCache))
            {
                return existingCache;
            }

            var config = _multiConfig.Configurations.FirstOrDefault(c => c.CacheName.Equals(cachename, StringComparison.OrdinalIgnoreCase))
                         ?? _multiConfig.Configurations.FirstOrDefault(c => c.CacheName.Equals("Default", StringComparison.OrdinalIgnoreCase));

            if (config == null) throw new InvalidOperationException($"未找到配置: {cachename}");

            var connection = GetConnection(config);
            var db = connection.GetDatabase(config.DefaultDB);

            var newCache = new RedisCache(cachename, db, connection);
            _cachePolicyProvider.ApplyConfiguration(cachename, newCache);

            _caches.TryAdd(cachename, newCache);
            return newCache;
        }

        private ConnectionMultiplexer GetConnection(IRedisConfiguration config)
        {
            var lazyConnection = _connections.GetOrAdd(config.RedisConnection, connStr =>
            {
                return new Lazy<ConnectionMultiplexer>(() =>
                {
                    var options = ConfigurationOptions.Parse(connStr);
                    options.ConnectTimeout = config.IdleTimeout;
                    options.AllowAdmin = true;
                    return ConnectionMultiplexer.Connect(options);
                });
            });

            return lazyConnection.Value;
        }

        public void Dispose()
        {
            // 释放所有建立的连接
            foreach (var lazyConn in _connections.Values)
            {
                if (lazyConn.IsValueCreated)
                {
                    lazyConn.Value.Close();
                    lazyConn.Value.Dispose();
                }
            }
        }
    }

}
