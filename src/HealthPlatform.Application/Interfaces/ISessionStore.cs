namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Manages user session tokens in the distributed cache.
/// Sessions expire after 15 minutes of inactivity (sliding expiration).
/// </summary>
public interface ISessionStore
{
    /// <summary>Stores a session value for the given user. Overwrites if exists.</summary>
    Task SetSessionAsync(string userId, string sessionValue, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the session value for the given user.
    /// Returns <c>null</c> if the session has expired or does not exist.
    /// </summary>
    Task<string?> GetSessionAsync(string userId, CancellationToken ct = default);

    /// <summary>Removes the session for the given user (logout / invalidation).</summary>
    Task DeleteSessionAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Resets the TTL to 15 minutes (sliding window). Call on every authenticated
    /// request to keep the session alive during active use.
    /// </summary>
    Task RefreshTtlAsync(string userId, CancellationToken ct = default);
}
