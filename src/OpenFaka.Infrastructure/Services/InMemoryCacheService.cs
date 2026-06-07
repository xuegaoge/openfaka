using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services;

/// <summary>
/// 基于 ConcurrentDictionary 的内存缓存实现，作为 Redis 不可用时的降级方案。
/// 每个条目记录过期时间，Get 时惰性清理。
/// </summary>
public class InMemoryCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _store = new();
    // 按 key 缓存 SemaphoreSlim 以在内存模式下实现同 key 互斥。
    // 理论上会随唯一 key 数量增长，但 OpenFaka 的 key 空间有界（配置/分类/商品数），可接受。
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private readonly ILogger<InMemoryCacheService> _logger;

    public InMemoryCacheService(ILogger<InMemoryCacheService> logger)
    {
        _logger = logger;
        _logger.LogInformation("Using InMemoryCacheService (Redis fallback)");
    }

    public Task SetAsync(string key, string value, int expireSeconds = 300)
    {
        _store[key] = new CacheEntry(value, DateTime.UtcNow.AddSeconds(expireSeconds));
        return Task.CompletedTask;
    }

    public Task<string> GetAsync(string key)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult(entry.Value);
            _store.TryRemove(key, out _);
        }
        return Task.FromResult<string>(null);
    }

    public Task<T> GetAsync<T>(string key) where T : class
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult(JsonSerializer.Deserialize<T>(entry.Value));
            _store.TryRemove(key, out _);
        }
        return Task.FromResult<T>(null);
    }

    public Task RemoveAsync(params string[] keys)
    {
        foreach (var key in keys)
            _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string key)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
                return Task.FromResult(true);
            _store.TryRemove(key, out _);
        }
        return Task.FromResult(false);
    }

    public Task<long> IncrementAsync(string key, long value = 1)
    {
        var entry = _store.GetOrAdd(key, _ => new CacheEntry("0", DateTime.MaxValue));
        lock (entry)
        {
            var current = long.TryParse(entry.Value, out var v) ? v : 0;
            var result = current + value;
            entry.Value = result.ToString();
            return Task.FromResult(result);
        }
    }

    public Task<bool> SetNxAsync(string key, string value, int expireSeconds = 300)
    {
        var added = _store.TryAdd(key, new CacheEntry(value, DateTime.UtcNow.AddSeconds(expireSeconds)));
        return Task.FromResult(added);
    }

    public Task SetAsync<T>(string key, T value, int expireSeconds = 300) where T : class
    {
        return SetAsync(key, JsonSerializer.Serialize(value), expireSeconds);
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        var keys = _store.Keys.Where(k => k.StartsWith(prefix)).ToList();
        foreach (var key in keys)
            _store.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public async Task<string> GetOrSetAsync(string key, Func<Task<string>> factory, int expireSeconds = 300)
    {
        var cached = await GetAsync(key);
        if (!string.IsNullOrEmpty(cached))
            return cached;

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            cached = await GetAsync(key);
            if (!string.IsNullOrEmpty(cached))
                return cached;

            var value = await factory();
            if (!string.IsNullOrEmpty(value))
                await SetAsync(key, value, expireSeconds);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, int expireSeconds = 300) where T : class
    {
        var cached = await GetAsync<T>(key);
        if (cached != null)
            return cached;

        var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync();
        try
        {
            cached = await GetAsync<T>(key);
            if (cached != null)
                return cached;

            var value = await factory();
            if (value != null)
                await SetAsync(key, value, expireSeconds);
            return value;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private class CacheEntry
    {
        public string Value;
        public DateTime ExpiresAt;

        public CacheEntry(string value, DateTime expiresAt)
        {
            Value = value;
            ExpiresAt = expiresAt;
        }
    }
}
