using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork                  _uow;
    private readonly IPasswordHasher              _hasher;
    private readonly IJwtTokenService             _jwt;
    private readonly ISessionStore                _session;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ISessionStore session,
        ILogger<LoginCommandHandler> logger)
    {
        _uow     = uow;
        _hasher  = hasher;
        _jwt     = jwt;
        _session = session;
        _logger  = logger;
    }

    public async Task<LoginResult> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var userRepo = _uow.Repository<User>();
        var spec     = new UserByEmailSpecification(request.Email);
        var matches  = await userRepo.GetAsync(spec, cancellationToken);
        var user     = matches.FirstOrDefault();

        // ── 1. User not found — return generic message ────────────────────
        if (user is null)
        {
            _logger.LogWarning("Login failed: email not found.");
            return Fail("Invalid email or password.");
        }

        // ── 2. Account inactive ───────────────────────────────────────────
        if (!user.IsActive)
        {
            await WriteAuditAsync(
                user.Id, "LoginFailed", nameof(User), user.Id,
                new { reason = "account_inactive" }, cancellationToken);

            return Fail("Account is inactive.");
        }

        // ── 3. Password verification ──────────────────────────────────────
        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            await WriteAuditAsync(
                user.Id, "LoginFailed", nameof(User), user.Id,
                new { reason = "invalid_password" }, cancellationToken);

            _logger.LogWarning("Login failed: invalid password for user {UserId}.", user.Id);
            return Fail("Invalid email or password.");
        }

        // ── 4. Generate session + token pair ──────────────────────────────
        var sessionId = Guid.NewGuid();
        var tokenPair = _jwt.GenerateTokenPair(user, sessionId);

        // ── 5. Store Redis session (15-min sliding TTL) ───────────────────
        await _session.SetSessionAsync(
            user.Id.ToString(), sessionId.ToString(), cancellationToken);

        // ── 6. Store refresh token in Redis (7-day TTL) ───────────────────
        await _jwt.StoreRefreshTokenAsync(
            user.Id, tokenPair.RefreshToken, cancellationToken);

        // ── 7. Update LastLoginAt + save ──────────────────────────────────
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _uow.SaveChangesAsync(cancellationToken);

        // ── 8. Audit: successful login ────────────────────────────────────
        await WriteAuditAsync(
            user.Id, "LoginSucceeded", nameof(User), user.Id,
            new { sessionId = sessionId.ToString() }, cancellationToken);

        return new LoginResult(true,
            tokenPair.AccessToken,
            tokenPair.RefreshToken,
            tokenPair.ExpiresIn,
            null);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static LoginResult Fail(string error)
        => new(false, null, null, 0, error);

    /// <summary>
    /// Writes an explicit audit entry. Skips the DB write when the caller has
    /// no authenticated user (userId == Guid.Empty) to avoid an FK violation on
    /// <see cref="AuditLog.UserId"/>; logs to Serilog instead.
    /// </summary>
    private async Task WriteAuditAsync(
        Guid userId, string action, string entityType,
        Guid entityId, object details, CancellationToken ct)
    {
        if (userId == Guid.Empty)
        {
            _logger.LogInformation(
                "Auth audit (anonymous): {Action} on {EntityType}", action, entityType);
            return;
        }

        var auditRepo = _uow.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            Action       = action,
            EntityType   = entityType,
            EntityId     = entityId,
            Timestamp    = DateTimeOffset.UtcNow,
            Details      = JsonDocument.Parse(JsonSerializer.Serialize(details)),
            PreviousHash = null,
            CurrentHash  = string.Empty
        }, ct);

        await _uow.SaveChangesAsync(ct);
    }
}
