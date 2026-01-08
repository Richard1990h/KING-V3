// Cache Service Interface
namespace LittleHelperAI.API.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}

public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, (object Value, DateTime? Expiry)> _cache = new();
    private readonly object _lock = new();

    public Task<T?> GetAsync<T>(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.Expiry.HasValue || entry.Expiry.Value > DateTime.UtcNow)
                {
                    return Task.FromResult((T?)entry.Value);
                }
                _cache.Remove(key);
            }
            return Task.FromResult(default(T?));
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        lock (_lock)
        {
            var expiryTime = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _cache[key] = (value!, expiryTime);
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        lock (_lock)
        {
            _cache.Remove(key);
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (!entry.Expiry.HasValue || entry.Expiry.Value > DateTime.UtcNow)
                {
                    return Task.FromResult(true);
                }
                _cache.Remove(key);
            }
            return Task.FromResult(false);
        }
    }
}

public class RedisCacheService : ICacheService
{
    private readonly StackExchange.Redis.IConnectionMultiplexer _redis;
    private readonly StackExchange.Redis.IDatabase _db;

    public RedisCacheService(StackExchange.Redis.IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = redis.GetDatabase();
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var value = await _db.StringGetAsync(key);
        if (!value.HasValue)
            return default;
        return System.Text.Json.JsonSerializer.Deserialize<T>(value!);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }

    public async Task RemoveAsync(string key)
    {
        await _db.KeyDeleteAsync(key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return await _db.KeyExistsAsync(key);
    }
}
