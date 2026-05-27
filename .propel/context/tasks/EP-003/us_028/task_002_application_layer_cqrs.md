# Task 002: Application Layer — CQRS Commands & Queries

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-028 |
| **Epic** | EP-003 |
| **Layer** | Application (CQRS handlers, validators, specifications) |
| **Priority** | Medium |
| **Estimated Effort** | 75 minutes |
| **Dependencies** | Task 001 (`SlotSwapRequest` entity, `SlotSwapStatus` enum, DB migration applied) |

## Objective

Implement three CQRS operations:

1. **`GetSwappableSlotsQuery`** — returns anonymized booked appointments (time only)
   for the same provider as the requester, excluding the requester's own appointment.
2. **`InitiateSwapRequestCommand`** — creates a `SlotSwapRequest` (status `Pending`,
   expires in 24 h) after enforcing all business rules.
3. **`CancelSwapRequestCommand`** — transitions a `Pending` swap request to `Cancelled`
   (requester only).

## Acceptance Criteria Covered

- AC: Patient views list of other patients' booked slots (same provider, anonymized: shows only time)
- AC: Patient selects desired slot and initiates swap request
- AC: Swap request created with status "Pending" containing: requester, target slot, offered slot
- AC: Target patient NOT identified to requester (privacy)
- AC: Requester can cancel pending swap request
- AC: Only one active swap request per appointment allowed
- AC: Swap request expires after 24 hours if no response
- AC: Audit log entry for swap request creation (auto-stamped by `AuditSaveChangesInterceptor`)

---

## Implementation Steps

### 1. DTOs (Shared Result Types)

Create `src/HealthPlatform.Application/Features/SlotSwap/SwappableSlotDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Anonymized view of a booked appointment slot available for swap.
/// Patient identity is intentionally omitted for privacy.
/// </summary>
public sealed record SwappableSlotDto(
    Guid           AppointmentId,
    DateTimeOffset SlotTime);
```

Create `src/HealthPlatform.Application/Features/SlotSwap/SwapRequestDto.cs`:

```csharp
namespace HealthPlatform.Application.Features.SlotSwap;

public sealed record SwapRequestDto(
    Guid          SwapRequestId,
    DateTimeOffset RequesterSlotTime,
    DateTimeOffset TargetSlotTime,
    string        Status,
    DateTimeOffset ExpiresAt);
```

---

### 2. `GetSwappableSlotsQuery` + Handler

Create `src/HealthPlatform.Application/Features/SlotSwap/GetSwappableSlotsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Returns all booked appointment slots for a given provider that are eligible
/// for swap with the requester's appointment. The requester's own appointment
/// is excluded. Only slot times are returned — no patient identity is exposed.
/// </summary>
public sealed record GetSwappableSlotsQuery(
    Guid RequesterAppointmentId) : IRequest<IReadOnlyList<SwappableSlotDto>>;
```

Create `src/HealthPlatform.Application/Features/SlotSwap/GetSwappableSlotsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class GetSwappableSlotsQueryHandler
    : IRequestHandler<GetSwappableSlotsQuery, IReadOnlyList<SwappableSlotDto>>
{
    private readonly IUnitOfWork _uow;

    public GetSwappableSlotsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SwappableSlotDto>> Handle(
        GetSwappableSlotsQuery query,
        CancellationToken      ct)
    {
        // ── 1. Load requester's appointment ──────────────────────────────
        var requesterAppt = await _uow.Repository<Appointment>()
            .GetByIdAsync(query.RequesterAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), query.RequesterAppointmentId);

        // ── 2. Find booked appointments: same provider, not requester's ──
        var spec = new SwappableAppointmentsSpecification(
            requesterAppt.ProviderId,
            query.RequesterAppointmentId);

        var candidates = await _uow.Repository<Appointment>().GetAsync(spec, ct);

        // ── 3. Return anonymized DTOs (time only, no patient identity) ───
        return candidates
            .Select(a => new SwappableSlotDto(a.Id, a.SlotTime))
            .OrderBy(d => d.SlotTime)
            .ToList()
            .AsReadOnly();
    }
}
```

Create `src/HealthPlatform.Application/Features/SlotSwap/SwappableAppointmentsSpecification.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Persistence.Specifications;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches booked (Scheduled) appointments for a given provider, excluding
/// the requester's own appointment.
/// Walk-in appointments (IsWalkIn = true) are excluded — they have no fixed slot.
/// </summary>
internal sealed class SwappableAppointmentsSpecification : BaseSpecification<Appointment>
{
    public SwappableAppointmentsSpecification(Guid providerId, Guid excludeAppointmentId)
        : base(a => a.ProviderId == providerId
                 && a.Id != excludeAppointmentId
                 && !a.IsWalkIn
                 && a.Status == AppointmentStatus.Scheduled)
    {
    }
}
```

> **Note**: `BaseSpecification<T>` and `IRepository<T>.GetAsync(spec, ct)` follow
> the pattern already established in the codebase (see `UserByEmailSpecification`).

---

### 3. `InitiateSwapRequestCommand` + Validator + Handler

Create `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Initiates a slot swap request: the caller offers their current appointment
/// slot in exchange for the target appointment's slot.
/// </summary>
public sealed record InitiateSwapRequestCommand(
    Guid RequesterPatientId,
    Guid RequesterAppointmentId,
    Guid TargetAppointmentId) : IRequest<SwapRequestDto>;
```

Create `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

public sealed class InitiateSwapRequestCommandValidator
    : AbstractValidator<InitiateSwapRequestCommand>
{
    public InitiateSwapRequestCommandValidator()
    {
        RuleFor(x => x.RequesterPatientId).NotEmpty();
        RuleFor(x => x.RequesterAppointmentId).NotEmpty();
        RuleFor(x => x.TargetAppointmentId).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.RequesterAppointmentId != x.TargetAppointmentId)
            .WithMessage("Cannot initiate a swap request against your own appointment.")
            .WithName("TargetAppointmentId");
    }
}
```

Create `src/HealthPlatform.Application/Features/SlotSwap/InitiateSwapRequestCommandHandler.cs`:

```csharp
using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class InitiateSwapRequestCommandHandler
    : IRequestHandler<InitiateSwapRequestCommand, SwapRequestDto>
{
    private static readonly TimeSpan SwapTtl = TimeSpan.FromHours(24);

    private readonly IUnitOfWork                              _uow;
    private readonly ILogger<InitiateSwapRequestCommandHandler> _logger;

    public InitiateSwapRequestCommandHandler(
        IUnitOfWork                               uow,
        ILogger<InitiateSwapRequestCommandHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<SwapRequestDto> Handle(
        InitiateSwapRequestCommand command,
        CancellationToken          ct)
    {
        var apptRepo = _uow.Repository<Appointment>();
        var swapRepo = _uow.Repository<SlotSwapRequest>();

        // ── 1. Load and validate requester's appointment ──────────────────
        var requesterAppt = await apptRepo.GetByIdAsync(command.RequesterAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.RequesterAppointmentId);

        if (requesterAppt.PatientId != command.RequesterPatientId)
            throw new ForbiddenException("Appointment does not belong to the requesting patient.");

        if (requesterAppt.Status != AppointmentStatus.Scheduled || requesterAppt.IsWalkIn)
            throw new ConflictException("Only active scheduled appointments can initiate a swap.");

        // ── 2. Enforce one-active-swap-per-appointment ────────────────────
        var existingSpec = new ActiveSwapRequestByAppointmentSpecification(
            command.RequesterAppointmentId);
        var existing = await swapRepo.GetAsync(existingSpec, ct);

        if (existing.Count > 0)
            throw new ConflictException(
                "This appointment already has a pending swap request. " +
                "Cancel the existing request before creating a new one.");

        // ── 3. Load and validate target appointment ───────────────────────
        var targetAppt = await apptRepo.GetByIdAsync(command.TargetAppointmentId, ct)
            ?? throw new NotFoundException(nameof(Appointment), command.TargetAppointmentId);

        if (targetAppt.ProviderId != requesterAppt.ProviderId)
            throw new ConflictException("Swap target must be with the same provider.");

        if (targetAppt.Status != AppointmentStatus.Scheduled || targetAppt.IsWalkIn)
            throw new ConflictException("Target appointment is not eligible for swap.");

        // ── 4. Create the swap request ────────────────────────────────────
        var now     = DateTimeOffset.UtcNow;
        var request = new SlotSwapRequest
        {
            Id                    = Guid.NewGuid(),
            RequesterPatientId    = command.RequesterPatientId,
            RequesterAppointmentId = command.RequesterAppointmentId,
            TargetAppointmentId   = command.TargetAppointmentId,
            Status                = SlotSwapStatus.Pending,
            ExpiresAt             = now.Add(SwapTtl),
        };

        await swapRepo.AddAsync(request, ct);

        // ── 5. Audit log (AuditSaveChangesInterceptor stamps CreatedAt/UpdatedAt) ──
        var auditDetails = JsonSerializer.Serialize(new
        {
            RequesterAppointmentId = command.RequesterAppointmentId,
            TargetAppointmentId    = command.TargetAppointmentId,
            ExpiresAt              = request.ExpiresAt,
        });

        await _uow.Repository<AuditLog>().AddAsync(new AuditLog
        {
            UserId      = command.RequesterPatientId,
            Action      = "SlotSwapRequested",
            EntityType  = nameof(SlotSwapRequest),
            EntityId    = request.Id,
            Timestamp   = now,
            Details     = JsonDocument.Parse(auditDetails),
            CurrentHash = string.Empty,
        }, ct);

        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Swap request {SwapId} created by patient {PatientId} targeting appointment {TargetId}",
            request.Id, command.RequesterPatientId, command.TargetAppointmentId);

        return new SwapRequestDto(
            request.Id,
            requesterAppt.SlotTime,
            targetAppt.SlotTime,
            request.Status.ToString(),
            request.ExpiresAt);
    }
}
```

---

### 4. `CancelSwapRequestCommand` + Validator + Handler

Create `src/HealthPlatform.Application/Features/SlotSwap/CancelSwapRequestCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>Cancels a pending swap request initiated by the calling patient.</summary>
public sealed record CancelSwapRequestCommand(
    Guid RequesterPatientId,
    Guid SwapRequestId,
    string? Reason = null) : IRequest;
```

Create `src/HealthPlatform.Application/Features/SlotSwap/CancelSwapRequestCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class CancelSwapRequestCommandHandler
    : IRequestHandler<CancelSwapRequestCommand>
{
    private readonly IUnitOfWork                              _uow;
    private readonly ILogger<CancelSwapRequestCommandHandler> _logger;

    public CancelSwapRequestCommandHandler(
        IUnitOfWork                                uow,
        ILogger<CancelSwapRequestCommandHandler>  logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task Handle(CancelSwapRequestCommand command, CancellationToken ct)
    {
        var swapRepo = _uow.Repository<SlotSwapRequest>();

        var request = await swapRepo.GetByIdAsync(command.SwapRequestId, ct)
            ?? throw new NotFoundException(nameof(SlotSwapRequest), command.SwapRequestId);

        // ── Ownership check ───────────────────────────────────────────────
        if (request.RequesterPatientId != command.RequesterPatientId)
            throw new ForbiddenException("Cannot cancel a swap request you did not initiate.");

        // ── Status guard ──────────────────────────────────────────────────
        if (request.Status != SlotSwapStatus.Pending)
            throw new ConflictException(
                $"Swap request is already {request.Status} and cannot be cancelled.");

        request.Status             = SlotSwapStatus.Cancelled;
        request.CancellationReason = command.Reason;

        swapRepo.Update(request);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Swap request {SwapId} cancelled by patient {PatientId}",
            command.SwapRequestId, command.RequesterPatientId);
    }
}
```

---

### 5. `ActiveSwapRequestByAppointmentSpecification`

Create `src/HealthPlatform.Application/Features/SlotSwap/ActiveSwapRequestByAppointmentSpecification.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Persistence.Specifications;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches pending swap requests where the given appointment is the requester side.
/// Used to enforce the one-active-swap-per-appointment business rule.
/// </summary>
internal sealed class ActiveSwapRequestByAppointmentSpecification
    : BaseSpecification<SlotSwapRequest>
{
    public ActiveSwapRequestByAppointmentSpecification(Guid requesterAppointmentId)
        : base(r => r.RequesterAppointmentId == requesterAppointmentId
                 && r.Status == SlotSwapStatus.Pending)
    {
    }
}
```

---

### 6. Register Application Exceptions (if not already present)

Verify these exception types exist in `src/HealthPlatform.Application/`:

- `NotFoundException` — thrown when an entity is not found (HTTP 404)
- `ForbiddenException` — thrown on ownership violations (HTTP 403)
- `ConflictException` — thrown on business rule violations (HTTP 409)

If any are missing, create them following the pattern:

```csharp
namespace HealthPlatform.Application;

public sealed class ConflictException(string message) : Exception(message);
public sealed class ForbiddenException(string message) : Exception(message);
```

Confirm `GlobalExceptionHandler` in `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs`
maps these to the correct HTTP status codes.

---

## Definition of Done

- [ ] `SwappableSlotDto` and `SwapRequestDto` records created
- [ ] `GetSwappableSlotsQuery` + `GetSwappableSlotsQueryHandler` implemented
- [ ] `SwappableAppointmentsSpecification` implemented
- [ ] `InitiateSwapRequestCommand` + validator + handler implemented
- [ ] `CancelSwapRequestCommand` + handler implemented
- [ ] `ActiveSwapRequestByAppointmentSpecification` implemented
- [ ] All custom exceptions (`ConflictException`, `ForbiddenException`) present and mapped in `GlobalExceptionHandler`
- [ ] `dotnet build` succeeds with no errors
