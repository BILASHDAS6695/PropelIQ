namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Minimal, non-PHI session payload persisted in Redis.
/// </summary>
public sealed record SessionState(
    Guid UserId,
    string Role,
    DateTimeOffset LoginTimestamp,
    DateTimeOffset LastActivityTimestamp,
    Guid SessionId);
