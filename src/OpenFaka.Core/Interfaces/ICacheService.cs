namespace OpenFaka.Core.Interfaces;

/// <summary>
/// Redis 缓存服务接口
/// </summary>
public interface ICacheService
{
    Task SetAsync(string key, string value, int expireSeconds = 300);
    Task<string> GetAsync(string key);
    Task<T> GetAsync<T>(string key) where T : class;
    Task RemoveAsync(params string[] keys);
    Task<bool> ExistsAsync(string key);
    Task<long> IncrementAsync(string key, long value = 1);
    Task<bool> SetNxAsync(string key, string value, int expireSeconds = 300);
    Task SetAsync<T>(string key, T value, int expireSeconds = 300) where T : class;

    /// <summary>
    /// 按前缀删除缓存（Redis: SCAN + DEL，内存: 遍历删除）
    /// </summary>
    Task RemoveByPrefixAsync(string prefix);
}
