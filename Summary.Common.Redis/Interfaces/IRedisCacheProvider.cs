namespace Summary.Common.Redis.Interfaces
{
    public interface IRedisCacheProvider
    {
        IRedisCache GetRedisCache(string cachename);
    }
}