using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Summary.Common.Redis.Interfaces;

namespace Summary.Common.Redis.Impls
{
    public class CacheConfigurationProvider : ICacheConfigurationProvider
    {
        private readonly ConcurrentDictionary<string, Action<IRedisCacheOption>> _specificConfigs = new(StringComparer.OrdinalIgnoreCase);

        private Action<IRedisCacheOption>? _globalConfig;

        public void Configure(string cacheName, Action<IRedisCacheOption> action)
        {
            //_specificConfigs.AddOrUpdate(
            //    cacheName,
            //    action,
            //    (key, existingAction) => existingAction + action
            //);
            _specificConfigs[cacheName] = action;
        }

        public void ConfigureAll(Action<IRedisCacheOption> action)
        {
            if (_globalConfig == null)
            {
                _globalConfig = action;
            }
            else
            {
                _globalConfig += action;
            }
        }

        public void ApplyConfiguration(string cacheName, IRedisCacheOption target)
        {
            if (_specificConfigs.TryGetValue(cacheName, out var action))
            {
                action.Invoke(target);
                return;
            }
            _globalConfig?.Invoke(target);

        }
    }

    public class CacheConfiguration : ICacheConfiguration
    {
        public string CacheName { get; }

        public Action<IRedisCacheOption> Action { get; }

        public CacheConfiguration(string cacheName, Action<IRedisCacheOption> action)
        {
            CacheName = cacheName;
            Action = action;
        }
    }
}
