using System.Collections.Concurrent;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Cache;

/// <summary>
/// In-memory fallback implementation of <see cref="ISessionStore"/>.
/// Intended for local development only — sessions are lost on restart
/// and are not shared across instances.
/// </summary>
internal sealed class InMemorySessionStore : ISessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(15);

    private sealed record Entry(SessionState Session, DateTimeOffset ExpiresAt);

    private readonly ConcurrentDictionary<Guid, Entry> _sessions = new();
    private readonly ILogger<InMemorySessionStore> _logger;

    public InMemorySessionStore(ILogger<InMemorySessionStore> logger)
    {
        _logger = logger;
    }

    public Task SetSessionAsync(SessionState session, CancellationToken ct = default)
    {
        _sessions[session.UserId] = new Entry(session, DateTimeOffset.UtcNow.Add(SessionTtl));
        _logger.LogDebug("InMemorySessionStore: session set for user {UserId}", session.UserId);
        return Task.CompletedTask;
    }

    public Task<SessionState?> GetSessionAsync(Guid userId, CancellationToken ct = default)
    {
        if (_sessions.TryGetValue(userId, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
            return Task.FromResult<SessionState?>(entry.Session);

        _sessions.TryRemove(userId, out _);
        return Task.FromResult<SessionState?>(null);
    }

    public Task DeleteSessionAsync(Guid userId, CancellationToken ct = default)
    {
        _sessions.TryRemove(userId, out _);
        _logger.LogDebug("InMemorySessionStore: session deleted for user {UserId}", userId);
        return Task.CompletedTask;
    }

    public async Task RefreshActivityAsync(Guid userId, DateTimeOffset activityAt,
                                           CancellationToken ct = default)
    {
        var existing = await GetSessionAsync(userId, ct);
        if (existing is null)
            return;

        await SetSessionAsync(existing with { LastActivityTimestamp = activityAt }, ct);
    }
}
