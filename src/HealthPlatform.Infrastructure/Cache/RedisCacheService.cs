using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ICacheService"/>.
/// All operations catch <see cref="RedisException"/> and degrade gracefully:
/// Get returns null, Set/Delete are silent no-ops, Exists returns false.
/// </summary>
internal sealed class RedisCacheService : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDatabase _db;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer multiplexer,
                              ILogger<RedisCacheService> logger)
    {
        _db     = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
        where T : class
    {
        try
        {
            var value = await _db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>(value.ToString(), JsonOptions);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Get for key {Key}. Bypassing cache", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl,
                                   CancellationToken ct = default)
        where T : class
    {
        try
        {
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await _db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Set for key {Key}. Skipping cache write", key);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _db.KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Delete for key {Key}. Skipping", key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _db.KeyExistsAsync(key);
        }
        catch (RedisException ex)
        {
            _logger.LogWarning(ex, "Redis unavailable on Exists for key {Key}. Returning false", key);
            return false;
        }
    }
}
