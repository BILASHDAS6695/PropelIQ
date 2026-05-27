# Task 004: Change Password — Command, Validator, Handler & Endpoint

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-017 |
| **Epic** | EP-001 |
| **Layer** | Application (CQRS) + API |
| **Priority** | High |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001 — `PasswordHistory`, `CredentialExpiresAt` fields; `AccountSecuritySettings` options; `IPasswordHasher` |

## Objective

Implement the full password-change feature:
- `ChangePasswordCommand` + `ChangePasswordResult` records
- `ChangePasswordCommandValidator` (FluentValidation complexity rules)
- `ChangePasswordCommandHandler` — verifies current password, rejects reuse from history, hashes new password, rotates history, resets `CredentialExpiresAt`, audits
- `POST /api/auth/change-password` endpoint on `AuthController` (Patient-only, reads `userId` from JWT)

---

## Implementation Steps

### 1. `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommand.cs` — Create

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Auth;

public sealed record ChangePasswordCommand(
    Guid   UserId,
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword) : IRequest<ChangePasswordResult>;

public sealed record ChangePasswordResult(bool IsSuccess, string? Error);
```

### 2. `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommandValidator.cs` — Create

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Auth;

internal sealed class ChangePasswordCommandValidator
    : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(12)
            .WithMessage("New password must be at least 12 characters.")
            .Matches(@"[A-Z]").WithMessage("Must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Must contain at least one digit.")
            .Matches(@"[^a-zA-Z0-9]").WithMessage("Must contain at least one special character.");

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword)
            .WithMessage("Passwords do not match.");
    }
}
```

> **Note:** `ValidationBehavior<,>` in the MediatR pipeline picks up this validator automatically via assembly scan — no manual registration needed.

### 3. `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommandHandler.cs` — Create

```csharp
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
        foreach (var oldHash in user.PasswordHistory)
        {
            if (_hasher.Verify(request.NewPassword, oldHash))
                return Fail($"New password cannot match any of your last {_security.PasswordHistorySize} passwords.");
        }

        // Also check the current hash (handles users with no history yet).
        if (_hasher.Verify(request.NewPassword, user.PasswordHash))
            return Fail($"New password cannot match any of your last {_security.PasswordHistorySize} passwords.");

        // ── 3. Hash new password ──────────────────────────────────────────
        var newHash = _hasher.Hash(request.NewPassword);

        // ── 4. Rotate history — prepend current hash, trim to max size ────
        user.PasswordHistory.Insert(0, user.PasswordHash);
        if (user.PasswordHistory.Count > _security.PasswordHistorySize)
            user.PasswordHistory.RemoveAt(user.PasswordHistory.Count - 1);

        // ── 5. Persist new password + reset expiry ────────────────────────
        user.PasswordHash        = newHash;
        user.CredentialExpiresAt = DateTimeOffset.UtcNow
            .AddDays(_security.PasswordExpiryDays);

        // ── 6. Audit ──────────────────────────────────────────────────────
        var auditRepo = _uow.Repository<AuditLog>();
        await auditRepo.AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = user.Id,
            Action       = "PasswordChanged",
            EntityType   = nameof(User),
            EntityId     = user.Id,
            Timestamp    = DateTimeOffset.UtcNow,
            Details      = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                credentialExpiresAt = user.CredentialExpiresAt
            })),
            PreviousHash = null,
            CurrentHash  = string.Empty
        }, cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Password changed for user {UserId}. New expiry: {Expiry}.",
            user.Id, user.CredentialExpiresAt);

        return new ChangePasswordResult(true, null);
    }

    private static ChangePasswordResult Fail(string error)
        => new(false, error);
}
```

### 4. `src/HealthPlatform.Api/Controllers/AuthController.cs` — Add endpoint + DTO

**Add using:**
```csharp
using HealthPlatform.Api.Authorization;
```

**Add `ChangePasswordRequest` DTO** (at bottom with other DTOs):
```csharp
/// <summary>Payload for POST /api/auth/change-password.</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);
```

**Add endpoint** (after `Logout`, before the DTO block):
```csharp
/// <summary>
/// Changes the authenticated user's password and resets the 90-day expiry clock.
/// </summary>
/// <returns>
/// 204 No Content — password changed successfully.<br/>
/// 400 Bad Request — current password incorrect or new password reused from history.<br/>
/// 422 Unprocessable Entity — input validation failed.
/// </returns>
[HttpPost("change-password")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> ChangePassword(
    [FromBody] ChangePasswordRequest request,
    CancellationToken ct)
{
    var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

    if (!Guid.TryParse(userIdClaim, out var userId))
        return Unauthorized();

    var result = await _sender.Send(
        new ChangePasswordCommand(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmNewPassword),
        ct);

    if (!result.IsSuccess)
    {
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title  = "Password change failed.",
            Detail = result.Error
        });
    }

    return NoContent();
}
```

---

## API Contract

### POST /api/auth/change-password

**Request headers:** `Authorization: Bearer <access_token>` (Patient policy)

**Request body:**
```json
{
  "currentPassword": "OldP@ssw0rd!2",
  "newPassword":     "NewS3cure!Pass",
  "confirmNewPassword": "NewS3cure!Pass"
}
```

**Responses:**
- `204 No Content` — success
- `400 Bad Request` — wrong current password or history reuse
- `422 Unprocessable Entity` — validation failures (complexity rules)
- `401 Unauthorized` — no valid Patient JWT

---

## Affected Files

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommand.cs` | **Created** |
| `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommandValidator.cs` | **Created** |
| `src/HealthPlatform.Application/Features/Auth/ChangePasswordCommandHandler.cs` | **Created** |
| `src/HealthPlatform.Api/Controllers/AuthController.cs` | +endpoint + DTO |

---

## Acceptance Criteria

- [ ] `ChangePasswordCommand` and `ChangePasswordResult` records exist
- [ ] Validator enforces 12-char min, upper/lower/digit/special, confirm-match
- [ ] Handler rejects wrong current password with `400`
- [ ] Handler rejects reuse of any of the last N hashes (and current hash)
- [ ] Handler rotates `PasswordHistory` and trims to `PasswordHistorySize`
- [ ] `CredentialExpiresAt` reset to `UtcNow + 90 days` on success
- [ ] `PasswordChanged` audit record written
- [ ] Endpoint is `[Authorize(Policy = PolicyNames.Patient)]`
- [ ] `dotnet build` passes (0 errors)

## Verification

```powershell
cd src
dotnet build HealthPlatform.sln --no-restore
```
