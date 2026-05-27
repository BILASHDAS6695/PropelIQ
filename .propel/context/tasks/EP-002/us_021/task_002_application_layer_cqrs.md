# Task 002: Application Layer — CQRS Commands & Queries

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-021 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS handlers, specifications) |
| **Priority** | High |
| **Estimated Effort** | 75 minutes |
| **Dependencies** | Task 001 (`WalkIn` status, nullable `SlotId`, `QueuePosition`, `ArrivalTime`) |

## Objective

Implement three commands/queries: quick-create a patient profile (for
unregistered walk-in patients), register a walk-in appointment with automatic
queue position assignment, and query the provider's daily combined queue
(scheduled + walk-in patients).

## Acceptance Criteria Covered

- AC: Staff can create walk-in appointment (patient, provider, visit reason)
- AC: Walk-in gets status "WalkIn" and queue position assigned
- AC: Queue position = last position for provider today + 1
- AC: Walk-in does not consume a pre-defined slot
- AC: If patient not in system, staff can quick-create patient profile
- AC: Walk-in marked with arrival time (auto-set to current time)
- AC: Walk-in visible in provider's daily queue alongside scheduled patients
- AC: Audit log created automatically via `AuditSaveChangesInterceptor`

---

## Implementation Steps

### 1. `QuickCreatePatientCommand` + Validator + Handler

Create `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Patients;

/// <summary>
/// Creates a minimal patient record for an unregistered walk-in.
/// Generates a placeholder User (IsActive = false) so the PatientProfile
/// FK constraint is satisfied without issuing portal credentials.
/// </summary>
public sealed record QuickCreatePatientCommand(
    string   FirstName,
    string   LastName,
    DateOnly Dob,
    string?  Phone = null) : IRequest<QuickCreatePatientResult>;

public sealed record QuickCreatePatientResult(
    Guid PatientProfileId,
    Guid UserId);
```

Create `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Patients;

public sealed class QuickCreatePatientCommandValidator
    : AbstractValidator<QuickCreatePatientCommand>
{
    public QuickCreatePatientCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dob).NotEmpty()
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone is not null);
    }
}
```

Create `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Patients;

/// <summary>
/// Creates a placeholder User (no portal access) and a PatientProfile
/// for a walk-in patient who is not yet registered in the system.
/// </summary>
internal sealed class QuickCreatePatientCommandHandler
    : IRequestHandler<QuickCreatePatientCommand, QuickCreatePatientResult>
{
    private readonly IUnitOfWork _uow;

    public QuickCreatePatientCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<QuickCreatePatientResult> Handle(
        QuickCreatePatientCommand command,
        CancellationToken         ct)
    {
        // Generate a unique internal email — not used for login.
        var internalEmail = $"walkin-{Guid.NewGuid():N}@internal.local";

        var user = new User
        {
            Id          = Guid.NewGuid(),
            Email       = internalEmail,
            PasswordHash = string.Empty,   // no portal login
            Role        = UserRole.Patient,
            IsActive    = false            // cannot log in
        };

        var profile = new PatientProfile
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            FirstName = command.FirstName,
            LastName  = command.LastName,
            Dob       = command.Dob,
            Phone     = command.Phone
        };

        await _uow.Repository<User>().AddAsync(user, ct);
        await _uow.Repository<PatientProfile>().AddAsync(profile, ct);
        await _uow.SaveChangesAsync(ct);

        return new QuickCreatePatientResult(profile.Id, user.Id);
    }
}
```

---

### 2. `RegisterWalkInCommand` + Validator + Handler + DTO

Create `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record RegisterWalkInCommand(
    Guid    PatientId,
    Guid    ProviderId,
    string? VisitReason = null) : IRequest<WalkInConfirmationDto>;

public sealed record WalkInConfirmationDto(
    Guid   AppointmentId,
    Guid   PatientId,
    Guid   ProviderId,
    string ProviderName,
    int    QueuePosition,
    DateTimeOffset ArrivalTime,
    string Status);
```

Create `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class RegisterWalkInCommandValidator
    : AbstractValidator<RegisterWalkInCommand>
{
    public RegisterWalkInCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.VisitReason)
            .MaximumLength(500)
            .When(x => x.VisitReason is not null);
    }
}
```

Create `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Registers a walk-in appointment for an existing patient with a given provider.
///
/// Flow:
/// 1. Validate provider exists.
/// 2. Validate patient profile exists.
/// 3. Compute next queue position: MAX(queue_position) + 1 for provider today.
/// 4. Create Appointment: Status = WalkIn, IsWalkIn = true, SlotId = null,
///    ArrivalTime = SlotTime = UtcNow, QueuePosition = computed.
/// 5. Persist. Audit log entry created automatically by AuditSaveChangesInterceptor.
/// </summary>
internal sealed class RegisterWalkInCommandHandler
    : IRequestHandler<RegisterWalkInCommand, WalkInConfirmationDto>
{
    private readonly IUnitOfWork _uow;

    public RegisterWalkInCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<WalkInConfirmationDto> Handle(
        RegisterWalkInCommand command,
        CancellationToken     ct)
    {
        // ── 1. Validate provider ───────────────────────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(command.ProviderId, ct)
            ?? throw new NotFoundException(nameof(Provider), command.ProviderId);

        // ── 2. Validate patient profile ────────────────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByPatientIdSpecification(command.PatientId), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), command.PatientId);

        var patient = patientProfiles[0];

        // ── 3. Compute queue position ─────────────────────────────────────
        var today      = DateOnly.FromDateTime(DateTime.UtcNow);
        var queueItems = await _uow.Repository<Appointment>()
            .GetAsync(new WalkInQueuePositionSpecification(command.ProviderId, today), ct);

        int nextPosition = queueItems.Count == 0
            ? 1
            : queueItems.Max(a => a.QueuePosition ?? 0) + 1;

        // ── 4. Create walk-in appointment ─────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        var appointment = new Appointment
        {
            Id            = Guid.NewGuid(),
            PatientId     = patient.Id,
            ProviderId    = command.ProviderId,
            SlotId        = null,              // walk-ins have no pre-booked slot
            SlotTime      = now,
            ArrivalTime   = now,
            Status        = AppointmentStatus.WalkIn,
            IsWalkIn      = true,
            QueuePosition = nextPosition,
            VisitReason   = command.VisitReason
        };

        await _uow.Repository<Appointment>().AddAsync(appointment, ct);
        await _uow.SaveChangesAsync(ct);

        return new WalkInConfirmationDto(
            appointment.Id,
            patient.Id,
            provider.Id,
            provider.Name,
            nextPosition,
            now,
            appointment.Status.ToString());
    }
}
```

---

### 3. `GetProviderQueueQuery` + Handler + DTO

Create `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProviderQueueQuery(Guid ProviderId, DateOnly Date)
    : IRequest<IReadOnlyList<QueueEntryDto>>;

public sealed record QueueEntryDto(
    Guid           AppointmentId,
    Guid           PatientId,
    string         Status,
    DateTimeOffset AppointmentTime,
    int?           QueuePosition,
    string?        VisitReason,
    bool           IsWalkIn);
```

Create `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderQueueQueryHandler
    : IRequestHandler<GetProviderQueueQuery, IReadOnlyList<QueueEntryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProviderQueueQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<QueueEntryDto>> Handle(
        GetProviderQueueQuery query,
        CancellationToken     ct)
    {
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new ProviderQueueByDateSpecification(query.ProviderId, query.Date), ct);

        return appointments
            .Select(a => new QueueEntryDto(
                a.Id,
                a.PatientId,
                a.Status.ToString(),
                a.IsWalkIn ? (a.ArrivalTime ?? a.SlotTime) : a.SlotTime,
                a.QueuePosition,
                a.VisitReason,
                a.IsWalkIn))
            .ToList();
    }
}
```

---

### 4. Application-Layer Specifications

Create `src/HealthPlatform.Application/Features/Appointments/WalkInQueuePositionSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all WalkIn appointments for a provider on a given UTC calendar day.
/// Used to determine the next available queue position.
/// </summary>
internal sealed class WalkInQueuePositionSpecification : ISpecification<Appointment>
{
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public WalkInQueuePositionSpecification(Guid providerId, DateOnly date)
    {
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && a.Status     == AppointmentStatus.WalkIn
          && a.ArrivalTime >= _dayStart
          && a.ArrivalTime <= _dayEnd;

    public List<Expression<Func<Appointment, object>>> Includes           => [];
    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

Create `src/HealthPlatform.Application/Features/Providers/ProviderQueueByDateSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns all active appointments for a provider on a given UTC calendar day:
/// Scheduled (online bookings) and WalkIn (walk-ins), ordered by
/// QueuePosition ascending (nulls last via large sentinel), then SlotTime.
/// </summary>
internal sealed class ProviderQueueByDateSpecification : ISpecification<Appointment>
{
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public ProviderQueueByDateSpecification(Guid providerId, DateOnly date)
    {
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0, TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.ProviderId == _providerId
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.WalkIn
           || a.Status == AppointmentStatus.Booked)
          && a.SlotTime >= _dayStart
          && a.SlotTime <= _dayEnd;

    public List<Expression<Func<Appointment, object>>> Includes           => [];
    public Expression<Func<Appointment, object>>?      OrderBy           => a => a.SlotTime;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

Create `src/HealthPlatform.Application/Features/Appointments/PatientProfileByPatientIdSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns a PatientProfile by its own primary key (Id).
/// Distinct from PatientProfileByUserIdSpecification which queries by UserId.
/// </summary>
internal sealed class PatientProfileByPatientIdSpecification : ISpecification<PatientProfile>
{
    private readonly Guid _patientId;

    public PatientProfileByPatientIdSpecification(Guid patientId) => _patientId = patientId;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => p.Id == _patientId;

    public List<Expression<Func<PatientProfile, object>>> Includes           => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommand.cs` | New (includes `QuickCreatePatientResult`) |
| `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommandValidator.cs` | New |
| `src/HealthPlatform.Application/Features/Patients/QuickCreatePatientCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommand.cs` | New (includes `WalkInConfirmationDto`) |
| `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommandValidator.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/RegisterWalkInCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/WalkInQueuePositionSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/PatientProfileByPatientIdSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQuery.cs` | New (includes `QueueEntryDto`) |
| `src/HealthPlatform.Application/Features/Providers/GetProviderQueueQueryHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/ProviderQueueByDateSpecification.cs` | New |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

All 8 existing tests pass. Build succeeds.
