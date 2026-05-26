namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Generic distributed cache service.
/// Implementations must degrade gracefully when the cache is unavailable.
/// Key naming convention: cache:{entityType}:{id}
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value. Returns <c>null</c> if the key does not exist
    /// or the cache is unavailable.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class;

    /// <summary>Stores a value with the specified TTL. No-ops if cache is unavailable.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default)
        where T : class;

    /// <summary>Removes the key. No-ops if cache is unavailable.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if the key exists. Returns <c>false</c> if the cache
    /// is unavailable (fail-safe default).
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
