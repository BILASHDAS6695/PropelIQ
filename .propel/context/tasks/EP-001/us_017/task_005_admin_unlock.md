# Task 005: Admin Unlock User — Command, Handler & Endpoint

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-017 |
| **Epic** | EP-001 |
| **Layer** | Application (CQRS) + API |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 — `FailedLoginAttempts` and `LockoutEnd` fields on `User` |

## Objective

Give admins the ability to manually clear a lockout. The operation is **idempotent** — unlocking an already-unlocked account succeeds silently (no error). All unlock events are logged in the audit trail.

---

## Implementation Steps

### 1. `src/HealthPlatform.Application/Features/Admin/UnlockUserCommand.cs` — Create

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Admin;

public sealed record UnlockUserCommand(Guid UserId) : IRequest<UnlockUserResult>;

public sealed record UnlockUserResult(bool IsSuccess, string? Error);
```

### 2. `src/HealthPlatform.Application/Features/Admin/UnlockUserCommandHandler.cs` — Create

```csharp
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

        // Capture state before clearing (for audit).
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
```

### 3. `src/HealthPlatform.Api/Controllers/AdminUsersController.cs` — Add endpoint

Add after the `DeactivateUser` action:

```csharp
/// <summary>
/// Manually unlocks a locked user account, clearing the failed-attempt counter
/// and lockout expiry. This operation is idempotent — unlocking an already-unlocked
/// account returns 204 without error.
/// </summary>
/// <param name="userId">User ID to unlock.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 204 No Content — account unlocked (or was already unlocked).<br/>
/// 404 Not Found — user does not exist.
/// </returns>
[HttpPost("{userId:guid}/unlock")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> UnlockUser(Guid userId, CancellationToken ct)
{
    var result = await _sender.Send(new UnlockUserCommand(userId), ct);

    if (!result.IsSuccess)
    {
        return NotFound(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title  = "User not found.",
            Detail = result.Error
        });
    }

    return NoContent();
}
```

Add using at top of file:
```csharp
using HealthPlatform.Application.Features.Admin;
```

---

## API Contract

### POST /api/admin/users/{userId}/unlock

**Request headers:** `Authorization: Bearer <admin_access_token>` (Admin policy)

**Responses:**

| Status | Body | Condition |
|--------|------|-----------|
| `204 No Content` | — | Account unlocked (or was already unlocked) |
| `404 Not Found` | `ProblemDetails` | User ID does not exist |
| `401 Unauthorized` | — | No valid Admin JWT |
| `403 Forbidden` | — | Valid JWT but not Admin role |

---

## Audit Event

| Event | Trigger | Extra fields |
|-------|---------|-------------|
| `AccountUnlockedByAdmin` | Always on success | `wasLocked: bool`, `previousLockoutEnd: DateTimeOffset?` |

`wasLocked` distinguishes a "real" unlock from an idempotent no-op without adding separate code paths.

---

## Affected Files

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Admin/UnlockUserCommand.cs` | **Created** |
| `src/HealthPlatform.Application/Features/Admin/UnlockUserCommandHandler.cs` | **Created** |
| `src/HealthPlatform.Api/Controllers/AdminUsersController.cs` | +`UnlockUser` action |

---

## Acceptance Criteria

- [ ] `UnlockUserCommand` and `UnlockUserResult` records exist
- [ ] Handler resets `FailedLoginAttempts = 0` and `LockoutEnd = null`
- [ ] Handler writes `AccountUnlockedByAdmin` audit with `wasLocked` and `previousLockoutEnd`
- [ ] Endpoint is `POST /api/admin/users/{userId}/unlock` under `[Authorize(Policy = PolicyNames.Admin)]`
- [ ] Missing user → 404; any user (locked or not) → 204
- [ ] `dotnet build` passes (0 errors)

## Verification

```powershell
cd src
dotnet build HealthPlatform.sln --no-restore
```

## Integration Test Checklist

- [ ] Lock a test account by triggering 5 bad logins
- [ ] Call `POST /api/admin/users/{id}/unlock` with admin token → 204
- [ ] Subsequent login with correct password → 200 and `FailedLoginAttempts = 0` in DB
- [ ] Call unlock on already-unlocked account → 204 (idempotent)
- [ ] Call unlock with non-existent `userId` → 404
