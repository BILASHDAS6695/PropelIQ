using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Admin;

internal sealed class UnlockUserCommandHandler
    : IRequestHandler<UnlockUserCommand, UnlockUserResult>
{
    private readonly IUnitOfWork                       _uow;
    private readonly ILogger<UnlockUserCommandHandler> _logger;

    public UnlockUserCommandHandler(
        IUnitOfWork uow,
        ILogger<UnlockUserCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<UnlockUserResult> Handle(
        UnlockUserCommand request,
        CancellationToken cancellationToken)
    {
        var userRepo = _uow.Repository<User>();
        var user     = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "UnlockUser failed: user {UserId} not found.", request.UserId);
            return new UnlockUserResult(false, "User not found.");
        }

        // Capture previous state for the audit record before clearing.
        var wasLocked       = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        var previousEndTime = user.LockoutEnd;

        // ── Reset lockout ─────────────────────────────────────────────────
        user.FailedLoginAttempts = 0;
        user.LockoutEnd          = null;

        // ── Audit ─────────────────────────────────────────────────────────
        var auditRepo = _uow.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = request.UserId,
            Action       = "AccountUnlockedByAdmin",
            EntityType   = nameof(User),
            EntityId     = request.UserId,
            Timestamp    = DateTimeOffset.UtcNow,
            Details      = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                wasLocked,
                previousLockoutEnd = previousEndTime
            })),
            PreviousHash = null,
            CurrentHash  = string.Empty
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "User {UserId} unlocked by admin. Was locked: {WasLocked}.",
            request.UserId, wasLocked);

        return new UnlockUserResult(true, null);
    }
}
