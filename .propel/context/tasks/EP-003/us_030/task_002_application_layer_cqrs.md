# Task 002: Application Layer — CQRS for Staff Swap Mediation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-030 |
| **Epic** | EP-003 |
| **Layer** | Application (queries, commands, validators, handlers, specifications) |
| **Priority** | Low |
| **Estimated Effort** | 100 minutes |
| **Dependencies** | Task 001 of this story (extended `SlotSwapStatus`, `SlotSwapRequest` mediation fields, `Version` concurrency token); US-029 Task 001 (`SwapRequestWithAppointmentsSpecification`, `SwapResponseDto`, `IEmailSender`, `Notification` entity) |

## Objective

Implement the full CQRS stack for staff-mediated swap operations:

1. **`GetPendingSwapRequestsQuery`** — staff can see all pending swap requests including
   both patient names (staff-visible data not exposed to the patient-facing API).

2. **`StaffMediateSwapCommand`** — staff can force-approve (slots swapped atomically,
   bypassing target patient consent) or force-decline (both parties notified) a pending
   swap request. Both actions require a mandatory reason and produce an audit log entry.

3. **`StaffReassignSlotsCommand`** — staff initiates a three-way reassignment: the
   requester takes the target's existing slot, the target is reassigned to a staff-supplied
   available slot. All three slots and the swap request are updated atomically.

All commands guard against the optimistic concurrency edge case: if another staff member
mediates the same swap concurrently, a `DbUpdateConcurrencyException` surfaces and is
translated to a `ConflictException`.

## Acceptance Criteria Covered

- AC: Staff dashboard shows all pending swap requests (with both patient names)
- AC: Staff can force-approve a swap (bypasses target patient acceptance)
- AC: Staff can force-decline a swap (with reason)
- AC: Staff can initiate a three-way swap (manual reassignment of slots)
- AC: All staff override actions require a reason text
- AC: Override actions logged in audit trail with staff ID and reason
- AC: Patients notified of staff-mediated outcomes
- Edge case: Staff approves swap for deactivated patient → validation error
- Edge case: Staff overrides already-expired swap → validation error
- Edge case: Multiple staff try to mediate same swap → optimistic concurrency check

---

## Implementation Steps

### 1. Shared Result DTO — `StaffMediationResultDto`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffMediationResultDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Returned by all staff mediation commands (force-approve, force-decline,
/// three-way reassignment) to confirm the outcome.
/// </summary>
public sealed record StaffMediationResultDto(
    Guid            SwapRequestId,
    string          Status,
    Guid            MediatedByUserId,
    DateTimeOffset  OverriddenAt,
    DateTimeOffset? RequesterNewSlotTime,
    DateTimeOffset? TargetNewSlotTime);
```

---

### 2. Staff Query DTO — `PendingSwapRequestSummaryDto`

Create `src/HealthPlatform.Application/Features/SlotSwap/PendingSwapRequestSummaryDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff-only view of a pending slot swap request.
/// Both patient names are exposed because staff are authorized to see them.
/// </summary>
public sealed record PendingSwapRequestSummaryDto(
    Guid           SwapRequestId,
    Guid           RequesterPatientId,
    string         RequesterFullName,
    DateTimeOffset RequesterSlotTime,
    Guid           TargetPatientId,
    string         TargetFullName,
    DateTimeOffset TargetSlotTime,
    DateTimeOffset ExpiresAt);
```

---

### 3. Specification — `PendingSwapRequestsSpecification`

Create `src/HealthPlatform.Application/Features/SlotSwap/PendingSwapRequestsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads all <see cref="SlotSwapRequest"/> rows in <c>Pending</c> status,
/// eagerly including both appointment navigations and the requester's patient profile.
/// The target patient profile is fetched separately by the handler because the
/// repository spec infrastructure does not support nested (ThenInclude) paths.
/// </summary>
internal sealed class PendingSwapRequestsSpecification : ISpecification<SlotSwapRequest>
{
    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Status == SlotSwapStatus.Pending;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterPatient,
        r => r.RequesterAppointment,
        r => r.TargetAppointment,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           =>
        r => r.ExpiresAt;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
```

---

### 4. Query — `GetPendingSwapRequestsQuery`

Create `src/HealthPlatform.Application/Features/SlotSwap/GetPendingSwapRequestsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff query: retrieves all pending slot swap requests with both patient names visible.
/// </summary>
public sealed record GetPendingSwapRequestsQuery : IRequest<IReadOnlyList<PendingSwapRequestSummaryDto>>;
```

---

### 5. Query Handler — `GetPendingSwapRequestsQueryHandler`

Create `src/HealthPlatform.Application/Features/SlotSwap/GetPendingSwapRequestsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class GetPendingSwapRequestsQueryHandler
    : IRequestHandler<GetPendingSwapRequestsQuery, IReadOnlyList<PendingSwapRequestSummaryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPendingSwapRequestsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<PendingSwapRequestSummaryDto>> Handle(
        GetPendingSwapRequestsQuery request,
        CancellationToken           ct)
    {
        // ── 1. Load all pending swap requests (requester patient + both appts) ─
        var pending = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new PendingSwapRequestsSpecification(), ct);

        if (pending.Count == 0)
            return [];

        // ── 2. Collect distinct target patient IDs for batch fetch ────────
        var targetPatientIds = pending
            .Select(r => r.TargetAppointment.PatientId)
            .Distinct()
            .ToHashSet();

        // ── 3. Load target patient profiles ──────────────────────────────
        var targetProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification(targetPatientIds), ct);

        var targetProfileMap = targetProfiles.ToDictionary(p => p.Id);

        // ── 4. Project to staff-visible DTOs ─────────────────────────────
        var result = new List<PendingSwapRequestSummaryDto>(pending.Count);

        foreach (var r in pending)
        {
            var requester = r.RequesterPatient;
            targetProfileMap.TryGetValue(r.TargetAppointment.PatientId, out var target);

            result.Add(new PendingSwapRequestSummaryDto(
                SwapRequestId:      r.Id,
                RequesterPatientId: requester.Id,
                RequesterFullName:  $"{requester.FirstName} {requester.LastName}",
                RequesterSlotTime:  r.RequesterAppointment.SlotTime,
                TargetPatientId:    r.TargetAppointment.PatientId,
                TargetFullName:     target is not null
                                        ? $"{target.FirstName} {target.LastName}"
                                        : "Unknown",
                TargetSlotTime:     r.TargetAppointment.SlotTime,
                ExpiresAt:          r.ExpiresAt));
        }

        return result;
    }
}
```

---

### 6. New Specification — `PatientProfilesByIdsSpecification`

Create `src/HealthPlatform.Application/Features/SlotSwap/PatientProfilesByIdsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Loads <see cref="PatientProfile"/> rows whose <c>Id</c> is in the provided set.
/// Used by <see cref="GetPendingSwapRequestsQueryHandler"/> to batch-fetch target
/// patient profiles in a single round-trip.
/// </summary>
internal sealed class PatientProfilesByIdsSpecification : ISpecification<PatientProfile>
{
    private readonly HashSet<Guid> _ids;

    public PatientProfilesByIdsSpecification(HashSet<Guid> ids) => _ids = ids;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => _ids.Contains(p.Id);

    public List<Expression<Func<PatientProfile, object>>> Includes           => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 7. Command — `StaffMediateSwapCommand`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff command to force-approve or force-decline a pending slot swap request,
/// bypassing the target patient's consent.
/// </summary>
/// <param name="SwapRequestId">ID of the pending swap request to mediate.</param>
/// <param name="ForceApprove">
///   <c>true</c> to force-approve (slots are swapped immediately);
///   <c>false</c> to force-decline (swap is rejected on behalf of the target patient).
/// </param>
/// <param name="Reason">
///   Mandatory justification text. Stored on the swap request and in the audit log.
/// </param>
public sealed record StaffMediateSwapCommand(
    Guid   SwapRequestId,
    bool   ForceApprove,
    string Reason) : IRequest<StaffMediationResultDto>;
```

---

### 8. Validator — `StaffMediateSwapCommandValidator`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffMediateSwapCommandValidator
    : AbstractValidator<StaffMediateSwapCommand>
{
    public StaffMediateSwapCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for all staff override actions.")
            .MaximumLength(500);
    }
}
```

---

### 9. Handler — `StaffMediateSwapCommandHandler`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffMediateSwapCommandHandler
    : IRequestHandler<StaffMediateSwapCommand, StaffMediationResultDto>
{
    private readonly IUnitOfWork                                _uow;
    private readonly ICurrentUserService                        _currentUser;
    private readonly IEmailSender                               _email;
    private readonly ILogger<StaffMediateSwapCommandHandler>    _logger;

    public StaffMediateSwapCommandHandler(
        IUnitOfWork                              uow,
        ICurrentUserService                      currentUser,
        IEmailSender                             email,
        ILogger<StaffMediateSwapCommandHandler>  logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _email       = email;
        _logger      = logger;
    }

    public async Task<StaffMediationResultDto> Handle(
        StaffMediateSwapCommand command,
        CancellationToken       ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "User must be authenticated to mediate a swap request.");

        var staffUserId = _currentUser.UserId.Value;

        // ── 1. Load swap request with both appointments and requester profile ──
        var swapRequests = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new SwapRequestWithAppointmentsSpecification(command.SwapRequestId), ct);

        if (swapRequests.Count == 0)
            throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        var swapRequest   = swapRequests[0];
        var requesterAppt = swapRequest.RequesterAppointment;
        var targetAppt    = swapRequest.TargetAppointment;

        // ── 2. Status guard: must be Pending ─────────────────────────────
        if (swapRequest.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {swapRequest.Status} and cannot be overridden.");

        // ── 3. Expiry guard: staff cannot override an expired request ─────
        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException(
                "Swap request has expired and cannot be overridden. Create a new swap request.");

        // ── 4. Active patient guard (force-approve only) ──────────────────
        if (command.ForceApprove)
        {
            var requesterUser = await _uow.Repository<User>()
                .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);
            var targetUser = await _uow.Repository<User>()
                .GetByIdAsync(targetAppt.PatientId, ct);

            // Load target patient profile to get UserId
            var targetProfiles = await _uow.Repository<PatientProfile>()
                .GetAsync(new PatientProfilesByIdsSpecification(
                    [targetAppt.PatientId]), ct);

            if (targetProfiles.Count > 0)
            {
                var targetPatientUser = await _uow.Repository<User>()
                    .GetByIdAsync(targetProfiles[0].UserId, ct);
                if (targetPatientUser is not null && !targetPatientUser.IsActive)
                    throw new ConflictException(
                        "Cannot force-approve a swap: the target patient's account is deactivated.");
            }

            if (requesterUser is not null && !requesterUser.IsActive)
                throw new ConflictException(
                    "Cannot force-approve a swap: the requester patient's account is deactivated.");
        }

        var now = DateTimeOffset.UtcNow;

        // ── 5. Apply the mediation outcome ────────────────────────────────
        DateTimeOffset? requesterNewSlotTime = null;
        DateTimeOffset? targetNewSlotTime    = null;

        if (command.ForceApprove)
        {
            // Atomically swap slot times (mirrors RespondToSwapRequestCommandHandler)
            var originalRequesterSlot = requesterAppt.SlotTime;
            var originalTargetSlot    = targetAppt.SlotTime;

            requesterAppt.SlotTime  = originalTargetSlot;
            targetAppt.SlotTime     = originalRequesterSlot;

            // Swap SlotId references so the slot availability index stays consistent
            var originalRequesterSlotId = requesterAppt.SlotId;
            requesterAppt.SlotId = targetAppt.SlotId;
            targetAppt.SlotId    = originalRequesterSlotId;

            _uow.Repository<Appointment>().Update(requesterAppt);
            _uow.Repository<Appointment>().Update(targetAppt);

            swapRequest.Status = SlotSwapStatus.StaffApproved;

            requesterNewSlotTime = requesterAppt.SlotTime;
            targetNewSlotTime    = targetAppt.SlotTime;
        }
        else
        {
            swapRequest.Status = SlotSwapStatus.StaffDeclined;
        }

        // ── 6. Stamp mediation metadata ───────────────────────────────────
        swapRequest.OverrideReason     = command.Reason;
        swapRequest.MediatedByUserId   = staffUserId;
        swapRequest.OverriddenAt       = now;
        _uow.Repository<SlotSwapRequest>().Update(swapRequest);

        // ── 7. Audit log ──────────────────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            Action               = command.ForceApprove ? "StaffForceApprove" : "StaffForceDecline",
            SwapRequestId        = command.SwapRequestId,
            Reason               = command.Reason,
            RequesterAppointmentId = requesterAppt.Id,
            TargetAppointmentId    = targetAppt.Id,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = staffUserId,
            Action       = command.ForceApprove ? "StaffForceApproveSwap" : "StaffForceDeclineSwap",
            EntityType   = nameof(SlotSwapRequest),
            EntityId     = swapRequest.Id,
            Timestamp    = now,
            Details      = JsonDocument.Parse(auditDetails),
            PreviousHash = null,
            CurrentHash  = string.Empty,
        }, ct);

        // ── 8. Save (optimistic concurrency: DbUpdateConcurrencyException
        //    surfaces here if another staff member updated the row first) ──
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Another staff member has already mediated this swap request. " +
                "Please refresh and try again.");
        }

        _logger.LogInformation(
            "Staff user {StaffUserId} {Action} swap request {SwapRequestId}. Reason: {Reason}",
            staffUserId,
            command.ForceApprove ? "force-approved" : "force-declined",
            command.SwapRequestId,
            command.Reason);

        // ── 9. Notify both patients ───────────────────────────────────────
        await NotifyBothPatientsAsync(
            command, swapRequest, requesterAppt, targetAppt,
            requesterNewSlotTime, targetNewSlotTime, now, ct);

        return new StaffMediationResultDto(
            SwapRequestId:       swapRequest.Id,
            Status:              swapRequest.Status.ToString(),
            MediatedByUserId:    staffUserId,
            OverriddenAt:        now,
            RequesterNewSlotTime: requesterNewSlotTime,
            TargetNewSlotTime:   targetNewSlotTime);
    }

    private async Task NotifyBothPatientsAsync(
        StaffMediateSwapCommand command,
        SlotSwapRequest         swapRequest,
        Appointment             requesterAppt,
        Appointment             targetAppt,
        DateTimeOffset?         requesterNewSlotTime,
        DateTimeOffset?         targetNewSlotTime,
        DateTimeOffset          now,
        CancellationToken       ct)
    {
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);

        var targetProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification([targetAppt.PatientId]), ct);

        User? targetUser = null;
        if (targetProfiles.Count > 0)
        {
            targetUser = await _uow.Repository<User>()
                .GetByIdAsync(targetProfiles[0].UserId, ct);
        }

        if (command.ForceApprove)
        {
            var requesterMsg =
                $"A staff member has approved your slot swap request. " +
                $"Your new appointment time is {requesterNewSlotTime:f} UTC.";

            var targetMsg =
                $"A staff member has reassigned your appointment slot. " +
                $"Your new appointment time is {targetNewSlotTime:f} UTC.";

            if (requesterUser is not null)
                await _email.SendAsync(requesterUser.Email,
                    "Your slot swap has been approved by staff", requesterMsg, ct);

            if (targetUser is not null)
                await _email.SendAsync(targetUser.Email,
                    "Your appointment slot has been updated by staff", targetMsg, ct);

            // In-app notifications
            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id            = Guid.NewGuid(),
                PatientId     = requesterAppt.PatientId,
                AppointmentId = requesterAppt.Id,
                Channel       = NotificationChannel.InApp,
                Type          = NotificationType.SlotSwap,
                SentAt        = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);

            await _uow.Repository<Notification>().AddAsync(new Notification
            {
                Id            = Guid.NewGuid(),
                PatientId     = targetAppt.PatientId,
                AppointmentId = targetAppt.Id,
                Channel       = NotificationChannel.InApp,
                Type          = NotificationType.SlotSwap,
                SentAt        = now,
                DeliveryStatus = DeliveryStatus.Sent,
            }, ct);
        }
        else
        {
            var requesterMsg =
                $"A staff member has declined your slot swap request. " +
                $"Reason: {command.Reason}. Your original appointment time is unchanged.";

            var targetMsg =
                $"A staff member has resolved a slot swap request that targeted your appointment. " +
                $"No change was made to your slot.";

            if (requesterUser is not null)
                await _email.SendAsync(requesterUser.Email,
                    "Your slot swap request was declined by staff", requesterMsg, ct);

            if (targetUser is not null)
                await _email.SendAsync(targetUser.Email,
                    "Slot swap request resolved — no change to your appointment", targetMsg, ct);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
```

> **Note on `DeliveryStatus`**: use the existing `DeliveryStatus` enum value from the
> domain. If a `Sent` value does not exist, use `DeliveryStatus.Delivered` or the
> nearest equivalent present in the enum.

---

### 10. Command — `StaffReassignSlotsCommand`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff command to perform a three-way slot reassignment:
/// the requester takes the target's current slot and the target is moved
/// to a staff-supplied available slot.
/// </summary>
/// <param name="SwapRequestId">Pending swap request to resolve via reassignment.</param>
/// <param name="NewTargetSlotId">
///   ID of an available <see cref="HealthPlatform.Domain.Entities.AppointmentSlot"/>
///   to assign to the target patient's appointment.
/// </param>
/// <param name="Reason">
///   Mandatory justification text. Stored on the swap request and in the audit log.
/// </param>
public sealed record StaffReassignSlotsCommand(
    Guid   SwapRequestId,
    Guid   NewTargetSlotId,
    string Reason) : IRequest<StaffMediationResultDto>;
```

---

### 11. Validator — `StaffReassignSlotsCommandValidator`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffReassignSlotsCommandValidator
    : AbstractValidator<StaffReassignSlotsCommand>
{
    public StaffReassignSlotsCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();
        RuleFor(c => c.NewTargetSlotId).NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for all staff override actions.")
            .MaximumLength(500);
    }
}
```

---

### 12. Handler — `StaffReassignSlotsCommandHandler`

Create `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffReassignSlotsCommandHandler
    : IRequestHandler<StaffReassignSlotsCommand, StaffMediationResultDto>
{
    private readonly IUnitOfWork                                    _uow;
    private readonly ICurrentUserService                            _currentUser;
    private readonly IEmailSender                                   _email;
    private readonly ILogger<StaffReassignSlotsCommandHandler>      _logger;

    public StaffReassignSlotsCommandHandler(
        IUnitOfWork                                  uow,
        ICurrentUserService                          currentUser,
        IEmailSender                                 email,
        ILogger<StaffReassignSlotsCommandHandler>    logger)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _email       = email;
        _logger      = logger;
    }

    public async Task<StaffMediationResultDto> Handle(
        StaffReassignSlotsCommand command,
        CancellationToken         ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException(
                "User must be authenticated to perform slot reassignment.");

        var staffUserId = _currentUser.UserId.Value;

        // ── 1. Load swap request with both appointments ───────────────────
        var swapRequests = await _uow.Repository<SlotSwapRequest>()
            .GetAsync(new SwapRequestWithAppointmentsSpecification(command.SwapRequestId), ct);

        if (swapRequests.Count == 0)
            throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        var swapRequest   = swapRequests[0];
        var requesterAppt = swapRequest.RequesterAppointment;
        var targetAppt    = swapRequest.TargetAppointment;

        // ── 2. Status guard: must be Pending ─────────────────────────────
        if (swapRequest.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {swapRequest.Status} and cannot be overridden.");

        // ── 3. Expiry guard ───────────────────────────────────────────────
        if (swapRequest.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ConflictException(
                "Swap request has expired and cannot be overridden.");

        // ── 4. Load and validate the new target slot ──────────────────────
        var newTargetSlot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.NewTargetSlotId, ct);

        if (newTargetSlot is null)
            throw new NotFoundException(nameof(AppointmentSlot), command.NewTargetSlotId);

        if (newTargetSlot.Status != SlotStatus.Available)
            throw new ConflictException(
                "The selected slot for the target patient is not available.");

        // Must be the same provider (three-way swap cannot change providers)
        if (newTargetSlot.ProviderId != targetAppt.ProviderId)
            throw new ConflictException(
                "The new target slot must be for the same provider as the original appointment.");

        // ── 5. Active patient guard ───────────────────────────────────────
        var requesterUser = await _uow.Repository<User>()
            .GetByIdAsync(swapRequest.RequesterPatient.UserId, ct);

        var targetProfileList = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfilesByIdsSpecification([targetAppt.PatientId]), ct);

        User? targetUser = null;
        if (targetProfileList.Count > 0)
        {
            targetUser = await _uow.Repository<User>()
                .GetByIdAsync(targetProfileList[0].UserId, ct);
        }

        if (requesterUser is not null && !requesterUser.IsActive)
            throw new ConflictException(
                "Cannot reassign slots: the requester patient's account is deactivated.");

        if (targetUser is not null && !targetUser.IsActive)
            throw new ConflictException(
                "Cannot reassign slots: the target patient's account is deactivated.");

        var now = DateTimeOffset.UtcNow;

        // ── 6. Three-way slot reassignment ────────────────────────────────
        // Requester takes target's original slot
        var originalTargetSlotId   = targetAppt.SlotId;
        var originalTargetSlotTime = targetAppt.SlotTime;
        var originalRequesterSlotId = requesterAppt.SlotId;

        requesterAppt.SlotId   = originalTargetSlotId;
        requesterAppt.SlotTime = originalTargetSlotTime;

        // Target gets the new staff-supplied slot
        targetAppt.SlotId   = newTargetSlot.Id;
        targetAppt.SlotTime = newTargetSlot.StartTime;

        // Update slot availability: new slot becomes Booked; requester's original slot freed
        newTargetSlot.Status = SlotStatus.Booked;
        _uow.Repository<AppointmentSlot>().Update(newTargetSlot);

        // Release the requester's original slot if it was a regular slot (not walk-in)
        if (originalRequesterSlotId.HasValue)
        {
            var releasedSlot = await _uow.Repository<AppointmentSlot>()
                .GetByIdAsync(originalRequesterSlotId.Value, ct);
            if (releasedSlot is not null)
            {
                releasedSlot.Status = SlotStatus.Available;
                _uow.Repository<AppointmentSlot>().Update(releasedSlot);
            }
        }

        _uow.Repository<Appointment>().Update(requesterAppt);
        _uow.Repository<Appointment>().Update(targetAppt);

        // ── 7. Stamp mediation metadata ───────────────────────────────────
        swapRequest.Status                  = SlotSwapStatus.StaffReassigned;
        swapRequest.OverrideReason          = command.Reason;
        swapRequest.MediatedByUserId        = staffUserId;
        swapRequest.OverriddenAt            = now;
        swapRequest.ThreeWayNewTargetSlotId = command.NewTargetSlotId;
        _uow.Repository<SlotSwapRequest>().Update(swapRequest);

        // ── 8. Audit log ──────────────────────────────────────────────────
        var auditDetails = JsonSerializer.Serialize(new
        {
            Action                  = "StaffThreeWayReassign",
            SwapRequestId           = command.SwapRequestId,
            Reason                  = command.Reason,
            RequesterAppointmentId  = requesterAppt.Id,
            TargetAppointmentId     = targetAppt.Id,
            NewTargetSlotId         = command.NewTargetSlotId,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            Id           = Guid.NewGuid(),
            UserId       = staffUserId,
            Action       = "StaffThreeWaySlotReassign",
            EntityType   = nameof(SlotSwapRequest),
            EntityId     = swapRequest.Id,
            Timestamp    = now,
            Details      = JsonDocument.Parse(auditDetails),
            PreviousHash = null,
            CurrentHash  = string.Empty,
        }, ct);

        // ── 9. Save (optimistic concurrency guard) ────────────────────────
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException(
                "Another staff member has already mediated this swap request. " +
                "Please refresh and try again.");
        }

        _logger.LogInformation(
            "Staff user {StaffUserId} performed three-way reassignment on swap request " +
            "{SwapRequestId}. NewTargetSlotId: {NewTargetSlotId}. Reason: {Reason}",
            staffUserId, command.SwapRequestId, command.NewTargetSlotId, command.Reason);

        // ── 10. Notify both patients ──────────────────────────────────────
        var requesterMsg =
            $"A staff member has reassigned your appointment slot as part of a scheduling adjustment. " +
            $"Your new appointment time is {requesterAppt.SlotTime:f} UTC.";

        var targetMsg =
            $"A staff member has reassigned your appointment slot as part of a scheduling adjustment. " +
            $"Your new appointment time is {targetAppt.SlotTime:f} UTC.";

        if (requesterUser is not null)
            await _email.SendAsync(requesterUser.Email,
                "Your appointment slot has been updated by staff", requesterMsg, ct);

        if (targetUser is not null)
            await _email.SendAsync(targetUser.Email,
                "Your appointment slot has been updated by staff", targetMsg, ct);

        await _uow.SaveChangesAsync(ct);

        return new StaffMediationResultDto(
            SwapRequestId:        swapRequest.Id,
            Status:               swapRequest.Status.ToString(),
            MediatedByUserId:     staffUserId,
            OverriddenAt:         now,
            RequesterNewSlotTime: requesterAppt.SlotTime,
            TargetNewSlotTime:    targetAppt.SlotTime);
    }
}
```

---

## Files Created

| Action | Path |
|--------|------|
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffMediationResultDto.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/PendingSwapRequestSummaryDto.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/PendingSwapRequestsSpecification.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/PatientProfilesByIdsSpecification.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/GetPendingSwapRequestsQuery.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/GetPendingSwapRequestsQueryHandler.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommand.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommandValidator.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffMediateSwapCommandHandler.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommand.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommandValidator.cs` |
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/StaffReassignSlotsCommandHandler.cs` |

## Verification

- `dotnet build src/HealthPlatform.sln` → 0 errors
- `GET /api/staff/swap-requests/pending` with Staff JWT → 200 with list of pending swaps including both patient names
- `POST /api/staff/swap-requests/{id}/mediate` with `ForceApprove = true` → both appointments' `SlotTime`/`SlotId` are swapped, status = `StaffApproved`, audit log row created
- `POST /api/staff/swap-requests/{id}/mediate` with `ForceApprove = false` → status = `StaffDeclined`, both parties emailed, audit log row created
- `POST /api/staff/swap-requests/reassign` → requester gets target slot, target gets new slot, `StaffReassigned` status, audit log row, both parties emailed
- Missing `Reason` field → 422 validation error
- Force-approve a swap for a deactivated patient → 409 Conflict
- Override an expired swap → 409 Conflict
- Concurrent staff mediation on the same row → `DbUpdateConcurrencyException` → 409 Conflict
