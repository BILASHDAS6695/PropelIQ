using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class RefreshTokenCommandHandler
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    private readonly IUnitOfWork                         _uow;
    private readonly IJwtTokenService                    _jwt;
    private readonly ISessionStore                       _session;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUnitOfWork uow,
        IJwtTokenService jwt,
        ISessionStore session,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _uow     = uow;
        _jwt     = jwt;
        _session = session;
        _logger  = logger;
    }

    public async Task<RefreshTokenResult> Handle(
        RefreshTokenCommand request,
        CancellationToken cancellationToken)
    {
        // ── 1. Validate & consume the refresh token (single-use) ──────────
        var valid = await _jwt.ValidateAndConsumeRefreshTokenAsync(
            request.UserId, request.RefreshToken, cancellationToken);

        if (!valid)
        {
            _logger.LogWarning(
                "Token refresh failed: invalid or expired refresh token for user {UserId}.",
                request.UserId);
            return Fail("Invalid or expired refresh token.");
        }

        // ── 2. Load user — verify account is still active ─────────────────
        var userRepo = _uow.Repository<User>();
        var user     = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Token refresh failed: user {UserId} not found or inactive.",
                request.UserId);
            return Fail("Account is unavailable.");
        }

        // ── 3. Generate new session ID + token pair ───────────────────────
        var newSessionId = Guid.NewGuid();
        var tokenPair    = _jwt.GenerateTokenPair(user, newSessionId);

        // ── 4. Update Redis session (resets the 15-min sliding TTL) ───────
        await _session.SetSessionAsync(
            user.Id.ToString(), newSessionId.ToString(), cancellationToken);

        // ── 5. Store new refresh token (7-day TTL, old already consumed) ──
        await _jwt.StoreRefreshTokenAsync(
            user.Id, tokenPair.RefreshToken, cancellationToken);

        return new RefreshTokenResult(
            true,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.ExpiresIn,
            null);
    }

    private static RefreshTokenResult Fail(string error)
        => new(false, null, null, 0, error);
}
