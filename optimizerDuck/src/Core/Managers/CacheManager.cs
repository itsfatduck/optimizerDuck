using Microsoft.Extensions.Caching.Memory;

namespace optimizerDuck.Core.Managers;

public static class CacheManager
{
    private static readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public static T GetOrCreate<T>(string key, Func<ICacheEntry, T> factory)
    {
        return _cache.GetOrCreate(key, factory)!;
    }
}