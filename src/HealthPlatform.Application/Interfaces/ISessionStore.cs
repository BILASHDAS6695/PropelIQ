namespace HealthPlatform.Application.Interfaces;

using HealthPlatform.Application.Features.Auth;

/// <summary>
/// Manages user session tokens in the distributed cache.
/// Sessions expire after 60 minutes of inactivity (sliding expiration).
/// </summary>
public interface ISessionStore
{
    /// <summary>Stores a structured session payload. Overwrites if exists.</summary>
    Task SetSessionAsync(SessionState session, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the session payload for the given user.
    /// Returns <c>null</c> if the session has expired or does not exist.
    /// </summary>
    Task<SessionState?> GetSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Removes the session for the given user (logout / invalidation).</summary>
    Task DeleteSessionAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Updates last activity and resets the TTL to 15 minutes (sliding window).
    /// Call on every authenticated request to keep the session alive during active use.
    /// </summary>
    Task RefreshActivityAsync(Guid userId, DateTimeOffset activityAt, CancellationToken ct = default);
}
