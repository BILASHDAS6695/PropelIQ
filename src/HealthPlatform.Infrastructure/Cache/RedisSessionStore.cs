using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ISessionStore"/>.
/// Key format: session:{userId}
/// TTL: 15 minutes (sliding — reset on every authenticated request via RefreshTtlAsync).
/// </summary>
internal sealed class RedisSessionStore : ISessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);
    private readonly IDatabase _db;
    private readonly ILogger<RedisSessionStore> _logger;

    public RedisSessionStore(IConnectionMultiplexer multiplexer,
                             ILogger<RedisSessionStore> logger)
    {
        _db     = multiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task SetSessionAsync(string userId, string sessionValue,
                                      CancellationToken ct = default)
    {
        var key = SessionKey(userId);
        await _db.StringSetAsync(key, sessionValue, SessionTtl);
        _logger.LogDebug("Session set for user {UserId}, TTL={Ttl}", userId, SessionTtl);
    }

    public async Task<string?> GetSessionAsync(string userId,
                                               CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(SessionKey(userId));
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task DeleteSessionAsync(string userId,
                                         CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(SessionKey(userId));
        _logger.LogDebug("Session deleted for user {UserId}", userId);
    }

    public async Task RefreshTtlAsync(string userId,
                                      CancellationToken ct = default)
    {
        await _db.KeyExpireAsync(SessionKey(userId), SessionTtl);
    }

    private static RedisKey SessionKey(string userId) => $"session:{userId}";
}
