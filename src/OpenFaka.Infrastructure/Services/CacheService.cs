using System.Text.Json;
using FreeRedis;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services;

public class RedisCacheService : ICacheService
{
    private readonly RedisClient _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(RedisClient redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public Task SetAsync(string key, string value, int expireSeconds = 300)
    {
        try
        {
            _redis.Set(key, value, expireSeconds);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Set failed for key: {Key}", key);
            return Task.CompletedTask;
        }
    }

    public Task<string> GetAsync(string key)
    {
        try
        {
            return Task.FromResult(_redis.Get(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Get failed for key: {Key}", key);
            return Task.FromResult<string>(null);
        }
    }

    public Task<T> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = _redis.Get(key);
            if (string.IsNullOrEmpty(value))
                return Task.FromResult<T>(null);
            return Task.FromResult(JsonSerializer.Deserialize<T>(value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Get failed for key: {Key}", key);
            return Task.FromResult<T>(null);
        }
    }

    public Task RemoveAsync(params string[] keys)
    {
        try
        {
            if (keys.Length > 0)
                _redis.Del(keys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Remove failed for keys: {Keys}", string.Join(",", keys));
        }
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        try
        {
            return Task.FromResult(_redis.Exists(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Exists failed for key: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task<long> IncrementAsync(string key, long value = 1)
    {
        try
        {
            return Task.FromResult(_redis.IncrBy(key, value));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis Increment failed for key: {Key}", key);
            return Task.FromResult(0L);
        }
    }

    public Task<bool> SetNxAsync(string key, string value, int expireSeconds = 300)
    {
        try
        {
            var result = _redis.SetNx(key, value);
            if (result && expireSeconds > 0)
                _redis.Expire(key, expireSeconds);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis SetNx failed for key: {Key}", key);
            return Task.FromResult(false);
        }
    }

    public Task SetAsync<T>(string key, T value, int expireSeconds = 300) where T : class
    {
        var json = JsonSerializer.Serialize(value);
        return SetAsync(key, json, expireSeconds);
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        try
        {
            long cursor = 0;
            var keysToDelete = new List<string>();
            do
            {
                var result = _redis.Scan(cursor, $"{prefix}*", 100, null);
                cursor = result.cursor;
                if (result.items?.Length > 0)
                    keysToDelete.AddRange(result.items);
            } while (cursor != 0);

            if (keysToDelete.Count > 0)
                _redis.Del(keysToDelete.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis RemoveByPrefix failed for prefix: {Prefix}", prefix);
        }
        return Task.CompletedTask;
    }

    public async Task<string> GetOrSetAsync(string key, Func<Task<string>> factory, int expireSeconds = 300)
    {
        var cached = await GetAsync(key);
        if (!string.IsNullOrEmpty(cached))
            return cached;

        var lockKey = $"lock:{key}";
        var lockValue = Guid.NewGuid().ToString("N");
        var lockAcquired = false;
        try
        {
            lockAcquired = await SetNxAsync(lockKey, lockValue, 30);
            if (lockAcquired)
            {
                cached = await GetAsync(key);
                if (!string.IsNullOrEmpty(cached))
                    return cached;

                var value = await factory();
                if (!string.IsNullOrEmpty(value))
                    await SetAsync(key, value, expireSeconds);
                return value;
            }

            await Task.Delay(100);
            cached = await GetAsync(key);
            if (!string.IsNullOrEmpty(cached))
                return cached;

            return await factory();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GetOrSet failed for key: {Key}", key);
            return await factory();
        }
        finally
        {
            if (lockAcquired)
                ReleaseLock(lockKey, lockValue);
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, int expireSeconds = 300) where T : class
    {
        var cached = await GetAsync<T>(key);
        if (cached != null)
            return cached;

        var lockKey = $"lock:{key}";
        var lockValue = Guid.NewGuid().ToString("N");
        var lockAcquired = false;
        try
        {
            lockAcquired = await SetNxAsync(lockKey, lockValue, 30);
            if (lockAcquired)
            {
                cached = await GetAsync<T>(key);
                if (cached != null)
                    return cached;

                var value = await factory();
                if (value != null)
                    await SetAsync(key, value, expireSeconds);
                return value;
            }

            await Task.Delay(100);
            cached = await GetAsync<T>(key);
            if (cached != null)
                return cached;

            return await factory();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis GetOrSet failed for key: {Key}", key);
            return await factory();
        }
        finally
        {
            if (lockAcquired)
                ReleaseLock(lockKey, lockValue);
        }
    }

    private void ReleaseLock(string lockKey, string lockValue)
    {
        try
        {
            // 仅当锁仍属于当前持有者时才删除，防止误删其他线程获得的锁
            _redis.Eval(
                "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                new[] { lockKey },
                new[] { lockValue });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis lock release failed for key: {LockKey}", lockKey);
        }
    }
}
