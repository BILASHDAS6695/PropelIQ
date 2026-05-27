# Task 001: Application Layer — CQRS (Respond + Initiation Notification)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-029 |
| **Epic** | EP-003 |
| **Layer** | Application (CQRS handlers, validators, specifications, notification side-effects) |
| **Priority** | Medium |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | US-028 Task 002 (`SlotSwapRequest` entity, `InitiateSwapRequestCommandHandler`, `SlotSwapStatus` enum, `IEmailSender`, `Notification` entity) |

## Objective

Implement three pieces:

1. **Extend `InitiateSwapRequestCommandHandler`** — after saving the new swap request,
   send an email and create an in-app `Notification` for the **target patient**, telling
   them about the incoming swap offer (time only — no requester identity).

2. **`RespondToSwapRequestCommand`** — allows the target patient to Accept or Decline
   a pending swap request. Acceptance swaps both appointments' `SlotId`/`SlotTime`
   atomically within a single EF transaction, notifies both parties.
   Decline records the reason and notifies the requester.

3. **`SwapRequestWithAppointmentsSpecification`** — loads a `SlotSwapRequest` including
   its two `Appointment` navigation properties and the `RequesterPatient`, allowing the
   handler to access slot times and patient IDs without additional round-trips.

## Acceptance Criteria Covered

- AC: Target patient receives notification (email + in-app) about swap request
- AC: Notification shows proposed new time, current time, provider name (no requester identity)
- AC: Target patient can Accept or Decline from notification or in-app
- AC: Accept: both appointments swap slot references atomically (single transaction)
- AC: Accept: both patients receive confirmation email with new time
- AC: Decline: requester notified, swap request status → "Declined"
- AC: Atomic swap: if either slot changes during execution → swap fails gracefully

---

## Implementation Steps

### 1. New Specification — `SwapRequestWithAppointmentsSpecification`

Create `src/HealthPlatform.Application/Features/SlotSwap/SwapRequestWithAppointmentsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads a <see cref="SlotSwapRequest"/> by ID, eagerly including both
/// appointment navigations and the requester's patient profile so that
/// the handler can access slot times and patient IDs in one trip.
/// </summary>
internal sealed class SwapRequestWithAppointmentsSpecification : ISpecification<SlotSwapRequest>
{
    private readonly Guid _swapRequestId;

    public SwapRequestWithAppointmentsSpecification(Guid swapRequestId) =>
        _swapRequestId = swapRequestId;

    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Id == _swapRequestId;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterAppointment,
        r => r.TargetAppointment,
        r => r.RequesterPatient,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           => null;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
```

---

### 2. New DTO — `SwapResponseDto`

Create `src/HealthPlatform.Application/Features/SlotSwap/SwapResponseDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Result returned after a target patient accepts or declines a swap request.
/// </summary>
public sealed record SwapResponseDto(
    Guid           SwapRequestId,
    string         Status,
    DateTimeOffset? RequesterNewSlotTime,
    DateTimeOffset? TargetNewSlotTime);
```

---

### 3. New Command — `RespondToSwapRequestCommand`

Create `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Issued by the target patient to accept or decline a pending swap request.
/// </summary>
/// <param name="SwapRequestId">ID of the swap request to respond to.</param>
/// <param name="Accept">
///   <c>true</c> to accept (slots are swapped); <c>false</c> to decline.
/// </param>
/// <param name="Reason">Optional decline reason. Ignored on accept.</param>
public sealed record RespondToSwapRequestCommand(
    Guid    SwapRequestId,
    bool    Accept,
    string? Reason = null) : IRequest<SwapResponseDto>;
```

---

### 4. New Validator — `RespondToSwapRequestCommandValidator`

Create `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class RespondToSwapRequestCommandValidator
    : AbstractValidator<RespondToSwapRequestCommand>
{
    public RespondToSwapRequestCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();

        RuleFor(c => c.Reason)
            .MaximumLength(500)
            .When(c => c.Reason is not null);
    }
}
```

---

### 5. New Handler — `RespondToSwapRequestCommandHandler`

Create `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class RespondToSwapRequestCommandHandler
    : IRequestHandler<RespondToSwapRequestCommand, SwapResponseDto>
{
    private readonly IUnitOfWork                                  _uow;
    private readonly ICurrentUserService                          _currentUser;
    private readonly IEmailSender                                 _email;
    private readonly ILogger<RespondToSwapRequestCommandHandler>  _logger;

    public RespondToSwapRequestCommandHandler(
        IUnitOfWork                                   uow,
        ICurrentUserService                           currentUser,
        IEmailSender                                  email,
        ILogger<RespondToSwapRequestCommandHandler>   logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _email       = email;
        _logger      = logger;
    }

    public async Task<SwapResponseDto> Handle(
        RespondToSwapRequestCommand command,
        CancellationToken           ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to respond to a swap request.");

        // ── 0. Resolve caller's patient profile ───────────────────────────
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var callerPatient = profiles[0];

        // ── 1. Load swap request with both appointments ────────────────────
        var swapRequests = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new SwapRequestWithAppointmentsSpecification(command.SwapRequestId), ct);

        if (swapRequests.Count == 0)
            throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        var swapRequest      = swapRequests[0];
        var requesterAppt    = swapRequest.RequesterAppointment;
        var targetAppt       = swapRequest.TargetAppointment;

        // ── 2. Ownership check: caller must own the TARGET appointment ─────
        if (targetAppt.PatientId != callerPatient.Id)
            throw new ForbiddenAccessException(
                "Only the target patient can respond to this swap request.");

        // ── 3. Status + expiry guards ─────────────────────────────────────
        if (swapRequest.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {swapRequest.Status} and cannot be responded to.");

        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException("Swap request has expired.");

        // ── 4. Load User entities for email addresses ─────────────────────
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);
        var targetUser = await _uow.Repository<User>()
            .GetByIdAsync(callerPatient.UserId, ct);

        var apptRepo = _uow.Repository<Appointment>();
        var now      = DateTimeOffset.UtcNow;

        // ── 5. Branch: Accept vs Decline ──────────────────────────────────
        if (command.Accept)
        {
            // Re-check both appointments are still eligible (concurrency guard)
            if (requesterAppt.Status != AppointmentStatus.Scheduled || requesterAppt.IsWalkIn)
                throw new ConflictException(
                    "Requester's appointment is no longer eligible for swap.");

            if (targetAppt.Status != AppointmentStatus.Scheduled || targetAppt.IsWalkIn)
                throw new ConflictException(
                    "Target appointment is no longer eligible for swap.");

            // Atomic slot swap
            (requesterAppt.SlotId,   targetAppt.SlotId)   = (targetAppt.SlotId,   requesterAppt.SlotId);
            (requesterAppt.SlotTime, targetAppt.SlotTime)  = (targetAppt.SlotTime, requesterAppt.SlotTime);

            apptRepo.Update(requesterAppt);
            apptRepo.Update(targetAppt);

            swapRequest.Status = SlotSwapStatus.Accepted;

            // Notify both patients
            var notifRepo = _uow.Repository<Notification>();

            await notifRepo.AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                PatientId      = swapRequest.RequesterPatientId,
                AppointmentId  = requesterAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);

            await notifRepo.AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                PatientId      = callerPatient.Id,
                AppointmentId  = targetAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);

            if (requesterUser is not null)
                await _email.SendAsync(
                    requesterUser.Email,
                    "Slot swap accepted — your appointment time has changed",
                    $"Your slot swap request was accepted. Your new appointment time is {requesterAppt.SlotTime:f} UTC.",
                    ct);

            if (targetUser is not null)
                await _email.SendAsync(
                    targetUser.Email,
                    "Slot swap confirmed — your appointment time has changed",
                    $"You accepted a slot swap. Your new appointment time is {targetAppt.SlotTime:f} UTC.",
                    ct);

            _logger.LogInformation(
                "Swap request {SwapId} accepted. Requester appt {ReqId} ↔ Target appt {TgtId}",
                command.SwapRequestId, requesterAppt.Id, targetAppt.Id);
        }
        else
        {
            swapRequest.Status             = SlotSwapStatus.Declined;
            swapRequest.CancellationReason = command.Reason;

            // Notify requester only
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id             = Guid.NewGuid(),
                PatientId      = swapRequest.RequesterPatientId,
                AppointmentId  = requesterAppt.Id,
                Channel        = NotificationChannel.Email,
                Type           = NotificationType.SlotSwap,
                SentAt         = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);

            if (requesterUser is not null)
                await _email.SendAsync(
                    requesterUser.Email,
                    "Slot swap declined",
                    "Your slot swap request was declined by the other patient.",
                    ct);

            _logger.LogInformation(
                "Swap request {SwapId} declined by target patient {PatientId}",
                command.SwapRequestId, callerPatient.Id);
        }

        _uow.Repository<SlotSwapRequest>().Update(swapRequest);
        await _uow.SaveChangesAsync(ct);

        return new SwapResponseDto(
            swapRequest.Id,
            swapRequest.Status.ToString(),
            command.Accept ? requesterAppt.SlotTime : null,
            command.Accept ? targetAppt.SlotTime    : null);
    }
}
```

---

### 6. Extend `InitiateSwapRequestCommandHandler` — Notify Target Patient

Edit `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommandHandler.cs`.

Add `IEmailSender _email` to constructor injection. After `await _uow.SaveChangesAsync(ct)`,
add the following block to notify the target patient (before the `return` statement):

```csharp
// ── 6. Notify target patient (email + in-app) ─────────────────────────
// Load target patient profile by their appointment's PatientId.
var targetPatientProfile = await _uow.Repository<PatientProfile>()
    .GetByIdAsync(targetAppt.PatientId, ct);

if (targetPatientProfile is not null)
{
    // In-app notification
    await _uow.Repository<Notification>().AddAsync(new Notification
    {
        Id             = Guid.NewGuid(),
        PatientId      = targetAppt.PatientId,
        AppointmentId  = targetAppt.Id,
        Channel        = NotificationChannel.Email,
        Type           = NotificationType.SlotSwap,
        SentAt         = now,
        DeliveryStatus = DeliveryStatus.Sent,
    }, ct);

    await _uow.SaveChangesAsync(ct);

    // Email notification (no requester identity exposed)
    var targetUser = await _uow.Repository<User>()
        .GetByIdAsync(targetPatientProfile.UserId, ct);

    if (targetUser is not null)
        await _email.SendAsync(
            targetUser.Email,
            "Someone wants to swap appointment slots with you",
            $"A patient has requested to swap slots. " +
            $"The proposed new time for your appointment is {requesterAppt.SlotTime:f} UTC. " +
            $"Your current appointment time is {targetAppt.SlotTime:f} UTC. " +
            "Log in to accept or decline.",
            ct);
}
```

**Constructor change** — add `IEmailSender email` parameter and field:

```csharp
private readonly IEmailSender _email;

// In constructor:
_email = email;
```

> **Note**: `Notification` and `User` usings are already in the handler's namespace.
> Add `using HealthPlatform.Domain.Entities;` if not already present.

---

## Files Modified / Created

| Action | Path |
|--------|------|
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/SwapRequestWithAppointmentsSpecification.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/SwapResponseDto.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommand.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommandValidator.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/RespondToSwapRequestCommandHandler.cs` |
| EDIT   | `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommandHandler.cs` |

## Verification

- `dotnet build src/HealthPlatform.Application` → 0 errors
- Send `POST .../swap-requests/{id}/respond` with `{ "accept": true }` → both `SlotTime`
  fields swap in the DB and emails are logged by `NoOpEmailSender`
- Send with `{ "accept": false, "reason": "time doesn't work" }` → status becomes
  `Declined`, only requester email is logged
- Calling as the **requester** (not target) → 403 Forbidden
- Responding to an already-`Accepted` swap → 409 Conflict
- Responding after `ExpiresAt` → 409 Conflict
