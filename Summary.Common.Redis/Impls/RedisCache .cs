using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StackExchange.Redis;
using Summary.Common.Model;
using Summary.Common.Redis.Interfaces;

namespace Summary.Common.Redis.Impls
{
    public class RedisCache : IRedisCache
    {
        private readonly IDatabase _db;
        private readonly ConnectionMultiplexer _connection;

        public string Name { get; set; }
        public TimeSpan DefaultSlidingExpireTime { get; set; } = TimeSpan.FromMinutes(20);
        public DateTimeOffset? DefaultAbsoluteExpireTime { get; set; }

        public ConcurrentDictionary<string, SemaphoreSlim> _locks = [];

        public RedisCache(string name, IDatabase db, ConnectionMultiplexer connection)
        {
            Name = name;
            _db = db;
            _connection = connection;
        }

        // ---------------------------------------------------------
        // [核心修改]：Key 构建器
        // 如果 Name 是 "User"，Key 是 "123"，则 Redis 中实际存储 "User:123"
        // ---------------------------------------------------------
        private string BuildKey(string key)
        {
            if (string.IsNullOrEmpty(Name)) return key;
            return $"{Name}:{key}";
        }

        // --- GetAsync ---
        public async Task<TValue?> GetAsync<TValue>(string key, Func<string, Task<TValue>>? factory = null)
        {
            var redisKey = BuildKey(key);
            var redisValue = await _db.StringGetAsync(redisKey);

            if (redisValue.HasValue)
            {
                return Deserialize<TValue>(redisValue);
            }

            if (!string.IsNullOrEmpty(key))
            {
                var myLock = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
                await myLock.WaitAsync();
                try
                {
                    // 双重检查，防止并发下重复创建
                    redisValue = await _db.StringGetAsync(redisKey);
                    if (redisValue.HasValue)
                    {
                        return Deserialize<TValue>(redisValue);
                    }
                    if (factory != null)
                    {
                        var value = await factory(key);
                        if (value != null)
                        {
                            await SetAsync(key, value);
                        }
                        return value;
                    }
                    return default;
                }
                finally
                {
                    myLock.Release();
                    // 可选：清理不再需要的锁，防止内存泄漏
                    if (myLock.CurrentCount == 1)
                    {
                        _locks.TryRemove(key, out _);
                    }
                }
            }

            return default;
        }

        public async IAsyncEnumerable<TValue?> GetAsync<TValue>(string[] keys, Func<string, Task<TValue>>? factory = null)
        {
            var redisKeys = keys.Select(k => (RedisKey)BuildKey(k)).ToArray();
            var redisValues = await _db.StringGetAsync(redisKeys);

            for (int i = 0; i < keys.Length; i++)
            {
                var val = redisValues[i];

                if (val.HasValue)
                {
                    yield return Deserialize<TValue>(val);
                    continue;
                }

                if (factory == null)
                {
                    yield return default;
                    continue;
                }

                var key = keys[i];
                var redisKey = redisKeys[i];

                var myLock = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));
                await myLock.WaitAsync();
                try
                {
                    var recheckVal = await _db.StringGetAsync(redisKey);
                    if (recheckVal.HasValue)
                    {
                        yield return Deserialize<TValue>(recheckVal);
                    }
                    else
                    {
                        var newValue = await factory(key);
                        if (newValue != null)
                        {
                            await SetAsync(key, newValue);
                        }
                        yield return newValue;
                    }
                }
                finally
                {
                    myLock.Release();
                    if (myLock.CurrentCount == 1)
                    {
                        _locks.TryRemove(key, out _);
                    }
                }
            }
        }

        public async Task<ConditionalValue<TValue>> GetValueOrDefaultAsync<TValue>(string key)
        {
            var redisValue = await _db.StringGetAsync(BuildKey(key));
            if (!redisValue.HasValue) return ConditionalValue<TValue>.NoValue;
            return ConditionalValue<TValue>.FromValue(Deserialize<TValue>(redisValue)!);
        }

        public async IAsyncEnumerable<ConditionalValue<TValue>> GetValueOrDefaultAsync<TValue>(string[] keys)
        {
            var redisKeys = keys.Select(k => (RedisKey)BuildKey(k)).ToArray();
            var values = await _db.StringGetAsync(redisKeys);

            foreach (var val in values)
            {
                if (!val.HasValue) yield return ConditionalValue<TValue>.NoValue;
                else yield return ConditionalValue<TValue>.FromValue(Deserialize<TValue>(val)!);
            }
        }

        // --- SetAsync ---
        public Task SetAsync<TValue>(string key, TValue value, TimeSpan? slidingExpireTime = null, DateTimeOffset? absoluteExpireTime = null)
        {
            var expiry = CalculateExpiry(slidingExpireTime, absoluteExpireTime);
            var json = Serialize(value);
            return _db.StringSetAsync(BuildKey(key), json, expiry, When.Always);
        }

        public async Task SetAsync<TValue>(KeyValuePair<string, TValue>[] pairs, TimeSpan? slidingExpireTime = null, DateTimeOffset? absoluteExpireTime = null)
        {
            var expiry = CalculateExpiry(slidingExpireTime, absoluteExpireTime);
            var batch = _db.CreateBatch();
            var tasks = new List<Task>();

            foreach (var pair in pairs)
            {
                var json = Serialize(pair.Value);
                tasks.Add(batch.StringSetAsync(BuildKey(pair.Key), json, expiry, When.Always));
            }

            batch.Execute();
            await Task.WhenAll(tasks);
        }

        // --- RemoveAsync ---
        public async Task<long> RemoveAsync(string key)
        {
            bool result = await _db.KeyDeleteAsync(BuildKey(key));
            return result ? 1 : 0;
        }

        public async IAsyncEnumerable<long> RemoveAsync(string[] keys)
        {
            var redisKeys = keys.Select(k => (RedisKey)BuildKey(k)).ToArray();
            var result = await _db.KeyDeleteAsync(redisKeys);
            yield return result;
        }

        // --- ClearAsync (特别注意) ---
        public async Task ClearAsync()
        {
            // 在前缀模式下，ClearAsync 不能直接 FlushDatabase，否则会误删其他模块的数据。
            // 必须通过 SCAN 命令找到所有匹配该前缀的 Key 进行删除。
            // 注意：这在生产环境（Key 数量巨大）时可能会有性能影响。

            if (string.IsNullOrEmpty(Name))
            {
                // 如果没有名字，认为是清空整个 DB（危险操作，视情况保留或移除）
                // await _db.ExecuteAsync("FLUSHDB"); 
                return;
            }

            var server = _connection.GetServer(_connection.GetEndPoints().First());
            var pattern = $"{Name}:*";

            var keys = server.KeysAsync(_db.Database, pattern);

            var keysToDelete = new List<RedisKey>();
            await foreach (var key in keys)
            {
                keysToDelete.Add(key);
                if (keysToDelete.Count >= 1000) // 分批删除，避免阻塞
                {
                    await _db.KeyDeleteAsync(keysToDelete.ToArray());
                    keysToDelete.Clear();
                }
            }

            if (keysToDelete.Count > 0)
            {
                await _db.KeyDeleteAsync(keysToDelete.ToArray());
            }
        }

        public void Dispose() { }

        // --- 辅助方法保持不变 ---
        private TimeSpan? CalculateExpiry(TimeSpan? sliding, DateTimeOffset? absolute)
        {
            if (absolute.HasValue) return absolute.Value - DateTimeOffset.UtcNow;
            if (sliding.HasValue) return sliding.Value;
            if (DefaultAbsoluteExpireTime.HasValue) return DefaultAbsoluteExpireTime.Value - DateTimeOffset.UtcNow;
            return DefaultSlidingExpireTime;
        }

        private string Serialize<T>(T value) => JsonSerializer.Serialize(value);
        private T? Deserialize<T>(RedisValue value) => value.HasValue ? JsonSerializer.Deserialize<T>(value.ToString()) : default;
    }
}
