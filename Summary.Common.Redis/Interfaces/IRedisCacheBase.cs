using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Summary.Common.Model;

namespace Summary.Common.Redis.Interfaces
{
    public interface IRedisCacheBase
    {
        string Name { get; set; }

        Task<TValue?> GetAsync<TValue>(string key, Func<string, Task<TValue>>? factory = null);

        IAsyncEnumerable<TValue?> GetAsync<TValue>(string[] keys, Func<string, Task<TValue>>? factory = null);

        Task<ConditionalValue<TValue>> GetValueOrDefaultAsync<TValue>(string key);

        IAsyncEnumerable<ConditionalValue<TValue>> GetValueOrDefaultAsync<TValue>(string[] keys);

        Task SetAsync<TValue>(string key, TValue value, TimeSpan? slidingExpireTime = null, DateTimeOffset? absoluteExpireTime = null);

        Task SetAsync<TValue>(KeyValuePair<string, TValue>[] pairs, TimeSpan? slidingExpireTime = null, DateTimeOffset? absoluteExpireTime = null);

        Task<long> RemoveAsync(string key);

        IAsyncEnumerable<long> RemoveAsync(string[] keys);

        Task ClearAsync();
    }

    public interface IRedisCacheOption
    {
        TimeSpan DefaultSlidingExpireTime { get; set; }

        DateTimeOffset? DefaultAbsoluteExpireTime { get; set; }
    }

    public interface IRedisCache : IRedisCacheBase, IRedisCacheOption, IDisposable
    {
    }
}