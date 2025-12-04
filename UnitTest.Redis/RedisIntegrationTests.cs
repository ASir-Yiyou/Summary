using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Summary.Common.Redis.Extensions;
using Summary.Common.Redis.Interfaces;

namespace UnitTest.Redis
{
    public class RedisIntegrationTests
    {
        private readonly IServiceProvider _serviceProvider;

        public RedisIntegrationTests()
        {
            var inMemorySettings = new Dictionary<string, string>
            {
                {"RedisConfig:0:CacheName", "Default"},
                {"RedisConfig:0:RedisConnection", "localhost:6379,abortConnect=false"},
                {"RedisConfig:0:DefaultDB", "0"},

                {"RedisConfig:1:CacheName", "User"},
                {"RedisConfig:1:RedisConnection", "localhost:6379,abortConnect=false"},
                {"RedisConfig:1:DefaultDB", "1"},
            };

            IConfiguration configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings!)
                .Build();

            var services = new ServiceCollection();

            // 2. 注册你的 Redis 服务
            // 这里的 "RedisConfig" 对应上面字典的 Key 前缀
            services.AddRedis(configuration, "RedisConfig", policy =>
            {
                // 配置全局策略：默认过期时间 2 秒 (方便测试过期)
                policy.ConfigureAll(opt => opt.DefaultSlidingExpireTime = TimeSpan.FromSeconds(2));

                // 配置特定策略：User 缓存 5 分钟过期
                policy.Configure("User", opt => opt.DefaultSlidingExpireTime = TimeSpan.FromMinutes(5));
            });

            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact] // 测试 DI 是否正常
        public void Service_Should_Be_Resolved_Successfully()
        {
            var provider = _serviceProvider.GetService<IRedisCacheProvider>();
            Assert.NotNull(provider);

            var defaultCache = provider.GetRedisCache("Default");
            Assert.NotNull(defaultCache);
        }

        [Fact] // 测试基本的 Set 和 Get
        public async Task Basic_Set_And_Get_Should_Work()
        {
            var provider = _serviceProvider.GetRequiredService<IRedisCacheProvider>();
            var cache = provider.GetRedisCache("Default");
            var key = "test_key_" + Guid.NewGuid();
            var value = "Hello World";

            // Action
            await cache.SetAsync(key, value);
            var result = await cache.GetAsync<string>(key);

            // Assert
            Assert.Equal(value, result);

            // Cleanup
            await cache.RemoveAsync(key);
        }

        [Fact] // 测试对象序列化
        public async Task Object_Serialization_Should_Work()
        {
            var provider = _serviceProvider.GetRequiredService<IRedisCacheProvider>();
            var cache = provider.GetRedisCache("Default");
            var key = "user_obj_" + Guid.NewGuid();
            var user = new TestUser { Id = 1, Name = "Admin", CreatedAt = DateTime.Now };

            // Action
            await cache.SetAsync(key, user);
            var result = await cache.GetAsync<TestUser>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Name, result.Name);
            Assert.Equal(user.Id, result.Id);
        }

        [Fact] // 测试多缓存隔离 (User vs Product)
        public async Task Different_CacheNames_Should_Be_Isolated()
        {
            var provider = _serviceProvider.GetRequiredService<IRedisCacheProvider>();

            // User Cache (配置中映射到 DB 1)
            var userCache = provider.GetRedisCache("User");
            // Product Cache (配置中没找到，会回退到 Default -> DB 0)
            var productCache = provider.GetRedisCache("Product");

            var sameKey = "id_1001";

            // Action: 向两个不同的 Cache 写入相同的 Key
            await userCache.SetAsync(sameKey, "I am a User");
            await productCache.SetAsync(sameKey, "I am a Product");

            // Assert
            var userValue = await userCache.GetAsync<string>(sameKey);
            var productValue = await productCache.GetAsync<string>(sameKey);

            // 验证互不干扰
            Assert.Equal("I am a User", userValue);
            Assert.Equal("I am a Product", productValue);

            // 原理验证：
            // userCache 生成的真实 Key 是 "User:id_1001" (且在 DB 1)
            // productCache 生成的真实 Key 是 "Product:id_1001" (且在 DB 0)
        }

        [Fact] // 测试过期策略配置
        public async Task Global_Expiration_Policy_Should_Work()
        {
            var provider = _serviceProvider.GetRequiredService<IRedisCacheProvider>();
            var tempCache = provider.GetRedisCache("TempData");

            var key = "expire_test";
            await tempCache.SetAsync(key, "data");

            var val1 = await tempCache.GetAsync<string>(key);
            Assert.NotNull(val1);

            await Task.Delay(2500);

            var val2 = await tempCache.GetAsync<string>(key);
            Assert.Null(val2);
        }
    }

    // 辅助测试类
    public class TestUser
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}