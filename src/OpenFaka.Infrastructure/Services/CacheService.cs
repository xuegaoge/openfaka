using System.Text.Json;
using FreeRedis;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services;

public class RedisCacheService : ICacheService, IDisposable
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
            var keys = _redis.Keys($"{prefix}*");
            if (keys != null && keys.Length > 0)
                _redis.Del(keys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis RemoveByPrefix failed for prefix: {Prefix}", prefix);
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
