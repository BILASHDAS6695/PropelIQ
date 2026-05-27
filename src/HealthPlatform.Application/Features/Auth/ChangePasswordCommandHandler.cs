using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class ChangePasswordCommandHandler
    : IRequestHandler<ChangePasswordCommand, ChangePasswordResult>
{
    private readonly IUnitOfWork                          _uow;
    private readonly IPasswordHasher                      _hasher;
    private readonly AccountSecuritySettings              _security;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUnitOfWork uow,
        IPasswordHasher hasher,
        IOptions<AccountSecuritySettings> security,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _uow      = uow;
        _hasher   = hasher;
        _security = security.Value;
        _logger   = logger;
    }

    public async Task<ChangePasswordResult> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        var userRepo = _uow.Repository<User>();
        var user     = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
            return Fail("User not found.");

        // ── 1. Verify current password ────────────────────────────────────
        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            _logger.LogWarning(
                "ChangePassword failed: current password incorrect for user {UserId}.",
                user.Id);
            return Fail("Current password is incorrect.");
        }

        // ── 2. Password history check ─────────────────────────────────────
        // Check stored history entries (most recent first).
        foreach (var oldHash in user.PasswordHistory)
        {
            if (_hasher.Verify(request.NewPassword, oldHash))
            {
                return Fail(
                    $"New password cannot match any of your last " +
                    $"{_security.PasswordHistorySize} passwords.");
            }
        }

        // Also check the current hash (handles users with no history yet).
        if (_hasher.Verify(request.NewPassword, user.PasswordHash))
        {
            return Fail(
                $"New password cannot match any of your last " +
                $"{_security.PasswordHistorySize} passwords.");
        }

        // ── 3. Hash new password ──────────────────────────────────────────
        var newHash = _hasher.Hash(request.NewPassword);

        // ── 4. Rotate history — prepend current hash, trim to max size ────
        user.PasswordHistory.Insert(0, user.PasswordHash);
        if (user.PasswordHistory.Count > _security.PasswordHistorySize)
        {
            user.PasswordHistory.RemoveAt(user.PasswordHistory.Count - 1);
        }

        // ── 5. Persist new password + reset expiry ────────────────────────
        user.PasswordHash        = newHash;
        user.CredentialExpiresAt = DateTimeOffset.UtcNow
            .AddDays(_security.PasswordExpiryDays);

        // ── 6. Audit ──────────────────────────────────────────────────────
        var auditRepo = _uow.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            Id          = Guid.NewGuid(),
            UserId      = user.Id,
            Action      = "PasswordChanged",
            EntityType  = nameof(User),
            EntityId    = user.Id,
            Timestamp   = DateTimeOffset.UtcNow,
            Details     = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                credentialExpiresAt = user.CredentialExpiresAt
            })),
            PreviousHash = null,
            CurrentHash  = string.Empty
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password changed for user {UserId}. Expires: {ExpiresAt}.",
            user.Id, user.CredentialExpiresAt);

        return new ChangePasswordResult(true, null);
    }

    private static ChangePasswordResult Fail(string error)
        => new(false, error);
}
