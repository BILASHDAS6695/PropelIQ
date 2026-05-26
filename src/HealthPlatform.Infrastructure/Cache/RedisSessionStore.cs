using System.Text.Json;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// Redis-backed implementation of <see cref="ISessionStore"/>.
/// Key format: session:{userId}
/// TTL: 15 minutes (sliding — reset on every authenticated request via RefreshActivityAsync).
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

    public async Task SetSessionAsync(SessionState session,
                                      CancellationToken ct = default)
    {
        var key = SessionKey(session.UserId);
        var payload = JsonSerializer.Serialize(session);
        await _db.StringSetAsync(key, payload, SessionTtl);
        _logger.LogDebug("Session set for user {UserId}, TTL={Ttl}", session.UserId, SessionTtl);
    }

    public async Task<SessionState?> GetSessionAsync(Guid userId,
                                                     CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(SessionKey(userId));
        if (value.IsNullOrEmpty)
            return null;

        try
        {
            return JsonSerializer.Deserialize<SessionState>(value!);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid session payload for user {UserId}", userId);
            return null;
        }
    }

    public async Task DeleteSessionAsync(Guid userId,
                                         CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(SessionKey(userId));
        _logger.LogDebug("Session deleted for user {UserId}", userId);
    }

    public async Task RefreshActivityAsync(Guid userId,
                                           DateTimeOffset activityAt,
                                           CancellationToken ct = default)
    {
        var existing = await GetSessionAsync(userId, ct);
        if (existing is null)
            return;

        var updated = existing with { LastActivityTimestamp = activityAt };
        await SetSessionAsync(updated, ct);
    }

    private static RedisKey SessionKey(Guid userId) => $"session:{userId}";
}
