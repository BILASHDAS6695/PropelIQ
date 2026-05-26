namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Holds the token pair issued after a successful login or refresh.
/// </summary>
/// <param name="AccessToken">Signed JWT valid for 30 minutes.</param>
/// <param name="RefreshToken">Opaque random token valid for 7 days (single-use).</param>
/// <param name="ExpiresIn">Access-token lifetime in seconds (1800).</param>
/// <param name="SessionId">Unique identifier for this login session, embedded in the JWT.</param>
public sealed record TokenResult(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    Guid   SessionId);
