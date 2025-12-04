using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using PracticalWork.Library.Abstractions.Storage;

namespace PracticalWork.Library.Cache.Redis;

/// <inheritdoc cref="IRedisService"/>
public class RedisService : IRedisService
{
    private readonly IDistributedCache _cache;
    private readonly List<string> _keys;

    public RedisService(IDistributedCache cache)
    {
        _cache = cache;
        _keys = [];
    }
    
    /// <inheritdoc cref="IRedisService.GetAsync{T}"/>
    public async Task<T> GetAsync<T>(string key)
    {
        var value = await _cache.GetStringAsync(key);
        return value == null ? default : JsonSerializer.Deserialize<T>(value);
    }

    /// <inheritdoc cref="IRedisService.SetAsync{T}"/>
    public async Task SetAsync<T>(string key, T value, TimeSpan expiration)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration
        };

        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options);
        
        _keys.Add(key);
    }

    /// <inheritdoc cref="IRedisService.RemoveAsync"/>
    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(key);
        
        _keys.Remove(key);
    }

    /// <inheritdoc cref="IRedisService.RemoveByPrefixAsync"/>
    public async Task RemoveByPrefixAsync(string prefix)
    {
        foreach (var key in _keys.Where(k => k.StartsWith(prefix)))
        {
            await _cache.RemoveAsync(key);
        }
    }
}