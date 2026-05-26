using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Generates JWT access tokens and manages single-use refresh tokens
/// stored in the distributed cache.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Builds a signed JWT access token and a cryptographically random
    /// refresh token for the given user and session.
    /// </summary>
    /// <param name="user">The authenticated user (email, role, and ID are embedded as claims).</param>
    /// <param name="sessionId">Unique session identifier embedded in the JWT <c>sid</c> claim.</param>
    /// <returns>A <see cref="TokenResult"/> containing both tokens and the expiry in seconds.</returns>
    TokenResult GenerateTokenPair(User user, Guid sessionId);

    /// <summary>
    /// Persists a refresh token in the distributed cache under the key
    /// <c>refresh:{userId}</c> with a 7-day TTL, overwriting any existing entry.
    /// </summary>
    Task StoreRefreshTokenAsync(
        Guid userId,
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Validates that the provided <paramref name="refreshToken"/> matches the
    /// cached value for <paramref name="userId"/>. If valid, atomically deletes
    /// the entry (single-use enforcement) and returns <c>true</c>. Returns
    /// <c>false</c> on mismatch or cache miss.
    /// </summary>
    Task<bool> ValidateAndConsumeRefreshTokenAsync(
        Guid   userId,
        string refreshToken,
        CancellationToken ct = default);

    /// <summary>
    /// Removes the refresh token for the given user (logout / account lockout).
    /// No-ops if the key does not exist.
    /// </summary>
    Task RevokeRefreshTokenAsync(Guid userId, CancellationToken ct = default);
}
