using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class LoginCommandHandler
    : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork                  _uow;
    private readonly IPasswordHasher              _hasher;
    private readonly IJwtTokenService             _jwt;
    private readonly ISessionStore                _session;
    private readonly AccountSecuritySettings      _security;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IJwtTokenService jwt,
        ISessionStore session,
        IOptions<AccountSecuritySettings> security,
        ILogger<LoginCommandHandler> logger)
    {
        _uow      = uow;
        _hasher   = hasher;
        _jwt      = jwt;
        _session  = session;
        _security = security.Value;
        _logger   = logger;
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

        var now = DateTimeOffset.UtcNow;

        // ── 2. Account inactive ───────────────────────────────────────────
        if (!user.IsActive)
        {
            await WriteAuditAsync(
                user.Id, "LoginFailed", nameof(User), user.Id,
                new { reason = "account_inactive" }, cancellationToken);

            return Fail("Account is inactive.");
        }

        // ── 3. Lockout gate ───────────────────────────────────────────────
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > now)
        {
            var remaining = (int)Math.Ceiling(
                (user.LockoutEnd.Value - now).TotalSeconds);

            await WriteAuditAsync(
                user.Id, "LoginBlockedByLockout", nameof(User), user.Id,
                new { lockoutEndsAt = user.LockoutEnd.Value, remainingSeconds = remaining },
                cancellationToken);

            return LockedOut(remaining);
        }

        // ── 4. Password verification ──────────────────────────────────────
        if (!_hasher.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;

            if (user.FailedLoginAttempts >= _security.MaxFailedLoginAttempts)
            {
                user.LockoutEnd = now.AddMinutes(_security.LockoutDurationMinutes);

                await WriteAuditAsync(
                    user.Id, "AccountLocked", nameof(User), user.Id,
                    new
                    {
                        failedAttempts = user.FailedLoginAttempts,
                        lockoutEnd     = user.LockoutEnd.Value
                    },
                    cancellationToken);

                _logger.LogWarning(
                    "Account {UserId} locked after {Attempts} failed attempts.",
                    user.Id, user.FailedLoginAttempts);
            }
            else
            {
                await WriteAuditAsync(
                    user.Id, "LoginFailed", nameof(User), user.Id,
                    new
                    {
                        reason         = "invalid_password",
                        failedAttempts = user.FailedLoginAttempts
                    },
                    cancellationToken);

                _logger.LogWarning(
                    "Login failed: invalid password for user {UserId}. Attempt {Attempt}/{Max}.",
                    user.Id, user.FailedLoginAttempts, _security.MaxFailedLoginAttempts);
            }

            await _uow.SaveChangesAsync(cancellationToken);
            return Fail("Invalid email or password.");
        }

        // ── 5. Successful authentication — reset lockout counter ──────────
        user.FailedLoginAttempts = 0;
        user.LockoutEnd          = null;

        // ── 6. Generate session + token pair ──────────────────────────────
        var sessionId = Guid.NewGuid();
        var tokenPair = _jwt.GenerateTokenPair(user, sessionId);

        // ── 7. Store Redis session (15-min sliding TTL) ───────────────────
        await _session.SetSessionAsync(
            new SessionState(
                user.Id,
                user.Role.ToString(),
                now,
                now,
                sessionId),
            cancellationToken);

        // ── 8. Store refresh token in Redis (7-day TTL) ────────────────────
        await _jwt.StoreRefreshTokenAsync(
            user.Id, tokenPair.RefreshToken, cancellationToken);

        // ── 9. Update LastLoginAt + save ──────────────────────────────────
        user.LastLoginAt = now;
        await _uow.SaveChangesAsync(cancellationToken);

        // ── 10. Audit: successful login ───────────────────────────────────
        await WriteAuditAsync(
            user.Id, "LoginSucceeded", nameof(User), user.Id,
            new { sessionId = sessionId.ToString() }, cancellationToken);

        // ── 11. Check password expiry ─────────────────────────────────────
        var passwordChangeRequired =
            user.CredentialExpiresAt.HasValue &&
            user.CredentialExpiresAt.Value <= now;

        return new LoginResult(
            IsSuccess:              true,
            AccessToken:            tokenPair.AccessToken,
            RefreshToken:           tokenPair.RefreshToken,
            ExpiresIn:              tokenPair.ExpiresIn,
            Error:                  null,
            PasswordChangeRequired: passwordChangeRequired);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static LoginResult Fail(string error)
        => new(false, null, null, 0, error);

    private static LoginResult LockedOut(int secondsRemaining)
        => new(false, null, null, 0,
            $"Account is locked. Try again in {secondsRemaining} seconds.",
            LockoutSecondsRemaining: secondsRemaining);

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
