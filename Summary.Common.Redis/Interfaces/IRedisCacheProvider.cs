using System;
using System.Collections.Generic;
using System.Text;

namespace Summary.Common.Redis.Interfaces
{
    public interface IRedisCacheProvider
    {
        IRedisCache GetRedisCache(string cachename);
    }
}
