# Task 002: Application Layer — CQRS Commands & Queries

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-020 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS handlers, specifications) |
| **Priority** | Critical |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (Appointment.VisitReason + Scheduled status + xmin on slot) |

## Objective

Implement `GetProvidersQuery` for provider listing with optional specialty
filter, and `BookAppointmentCommand` for the complete booking flow: patient
identity resolution, duplicate-booking guard, optimistic-concurrency slot
locking, atomic Appointment creation, and confirmation email dispatch.

## Acceptance Criteria Covered

- AC: Patient selects provider from list (filtered by specialty optional)
- AC: Patient selects slot + provides visit reason
- AC: Slot status → Booked, Appointment created with status Scheduled
- AC: Slot locked during booking (optimistic concurrency — DbUpdateConcurrencyException)
- AC: Confirmation email sent to patient
- AC: Booking creates audit log entry (automatic via AuditSaveChangesInterceptor)
- AC: Patient can only have one active appointment per provider per day

---

## Implementation Steps

### 1. `GetProvidersQuery` + Handler + DTO

Create `src/HealthPlatform.Application/Features/Providers/GetProvidersQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProvidersQuery(string? Specialty = null)
    : IRequest<IReadOnlyList<ProviderDto>>;

public sealed record ProviderDto(
    Guid    ProviderId,
    string  Name,
    string? Specialty);
```

Create `src/HealthPlatform.Application/Features/Providers/GetProvidersQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProvidersQueryHandler
    : IRequestHandler<GetProvidersQuery, IReadOnlyList<ProviderDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProvidersQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<ProviderDto>> Handle(
        GetProvidersQuery query,
        CancellationToken ct)
    {
        var providers = await _uow.Repository<Provider>()
            .GetAsync(new ProvidersBySpecialtySpecification(query.Specialty), ct);

        return providers
            .Select(p => new ProviderDto(p.Id, p.Name, p.Specialty))
            .ToList();
    }
}
```

---

### 2. `ProvidersBySpecialtySpecification`

Create `src/HealthPlatform.Application/Features/Providers/ProvidersBySpecialtySpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns all active providers, optionally filtered by specialty
/// (case-insensitive substring match). Orders by provider name ascending.
/// </summary>
internal sealed class ProvidersBySpecialtySpecification : ISpecification<Provider>
{
    private readonly string? _specialty;

    public ProvidersBySpecialtySpecification(string? specialty)
        => _specialty = specialty?.Trim().ToLowerInvariant();

    public Expression<Func<Provider, bool>>? Criteria =>
        string.IsNullOrEmpty(_specialty)
            ? null
            : p => p.Specialty != null
                && p.Specialty.ToLower().Contains(_specialty);

    public List<Expression<Func<Provider, object>>> Includes => [];
    public Expression<Func<Provider, object>>? OrderBy           => p => p.Name;
    public Expression<Func<Provider, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

### 3. `BookAppointmentCommand` + Validator

Create `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record BookAppointmentCommand(
    Guid    SlotId,
    string? VisitReason = null) : IRequest<BookingConfirmationDto>;

public sealed record BookingConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset AppointmentTime,
    string         Status);
```

Create `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class BookAppointmentCommandValidator
    : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.SlotId).NotEmpty();
        RuleFor(x => x.VisitReason)
            .MaximumLength(500)
            .When(x => x.VisitReason is not null);
    }
}
```

---

### 4. `BookAppointmentCommandHandler`

Create `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Books an appointment slot for the currently authenticated patient.
///
/// Flow:
/// 1. Resolve patient profile from ICurrentUserService.UserId.
/// 2. Load the requested slot — verify it is Available.
/// 3. Guard against duplicate active appointments on the same provider/day.
/// 4. Create Appointment (status = Scheduled) + mark slot Booked.
/// 5. SaveChanges — catches DbUpdateConcurrencyException for concurrent
///    bookings and surfaces a user-friendly error.
/// 6. Send confirmation email via IEmailSender.
/// </summary>
internal sealed class BookAppointmentCommandHandler
    : IRequestHandler<BookAppointmentCommand, BookingConfirmationDto>
{
    private readonly IUnitOfWork           _uow;
    private readonly ICurrentUserService   _currentUser;
    private readonly IEmailSender          _emailSender;

    public BookAppointmentCommandHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser,
        IEmailSender        emailSender)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _emailSender = emailSender;
    }

    public async Task<BookingConfirmationDto> Handle(
        BookAppointmentCommand command,
        CancellationToken      ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to book appointments.");

        // ── 1. Resolve patient profile ────────────────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (patientProfiles.Count == 0)
            throw new InvalidOperationException("Patient profile not found for the current user.");

        var patient = patientProfiles[0];

        // ── 2. Load slot ───────────────────────────────────────────────────
        var slot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.SlotId, ct)
            ?? throw new KeyNotFoundException($"Slot {command.SlotId} not found.");

        if (slot.Status != SlotStatus.Available)
            throw new InvalidOperationException("This slot is no longer available.");

        // ── 3. Duplicate-booking guard ─────────────────────────────────────
        // Patient may not have more than one active (Scheduled/Booked) appointment
        // with the same provider on the same calendar day.
        var slotDate = DateOnly.FromDateTime(slot.StartTime.UtcDateTime);
        var duplicates = await _uow.Repository<Appointment>()
            .GetAsync(
                new ActiveAppointmentByPatientProviderDateSpecification(
                    patient.Id, slot.ProviderId, slotDate), ct);

        if (duplicates.Count > 0)
            throw new InvalidOperationException(
                "You already have an active appointment with this provider on the requested date.");

        // ── 4. Load provider for confirmation DTO ──────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(slot.ProviderId, ct)
            ?? throw new KeyNotFoundException($"Provider {slot.ProviderId} not found.");

        // ── 5. Create appointment + mark slot Booked ───────────────────────
        var appointment = new Appointment
        {
            Id          = Guid.NewGuid(),
            PatientId   = patient.Id,
            ProviderId  = slot.ProviderId,
            SlotId      = slot.Id,
            SlotTime    = slot.StartTime,
            Status      = AppointmentStatus.Scheduled,
            VisitReason = command.VisitReason,
            IsWalkIn    = false
        };

        slot.Status = SlotStatus.Booked;
        _uow.Repository<AppointmentSlot>().Update(slot);
        await _uow.Repository<Appointment>().AddAsync(appointment, ct);

        // ── 6. Persist — first wins on concurrent booking ─────────────────
        try
        {
            await _uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                "This slot is no longer available. Another patient just booked it.");
        }

        // ── 7. Send confirmation email (fire-and-log, non-blocking) ───────
        var user = await _uow.Repository<User>()
            .GetByIdAsync(patient.UserId, ct);

        if (user is not null)
        {
            var emailBody =
                $"Your appointment with {provider.Name} is confirmed.\n" +
                $"Date & Time: {appointment.SlotTime:f} UTC\n" +
                $"Appointment ID: {appointment.Id}";

            await _emailSender.SendAsync(
                user.Email,
                "Appointment Confirmation",
                emailBody,
                ct);
        }

        return new BookingConfirmationDto(
            appointment.Id,
            provider.Id,
            provider.Name,
            appointment.SlotTime,
            appointment.Status.ToString());
    }
}
```

---

### 5. Application-Layer Specifications

Create `src/HealthPlatform.Application/Features/Appointments/PatientProfileByUserIdSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class PatientProfileByUserIdSpecification : ISpecification<PatientProfile>
{
    private readonly Guid _userId;

    public PatientProfileByUserIdSpecification(Guid userId) => _userId = userId;

    public Expression<Func<PatientProfile, bool>>? Criteria =>
        p => p.UserId == _userId;

    public List<Expression<Func<PatientProfile, object>>> Includes => [];
    public Expression<Func<PatientProfile, object>>?      OrderBy           => null;
    public Expression<Func<PatientProfile, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

Create `src/HealthPlatform.Application/Features/Appointments/ActiveAppointmentByPatientProviderDateSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns active (Scheduled or Booked) appointments for a patient with a
/// specific provider on a given UTC calendar day.
/// Used to enforce the one-active-appointment-per-provider-per-day rule.
/// </summary>
internal sealed class ActiveAppointmentByPatientProviderDateSpecification
    : ISpecification<Appointment>
{
    private readonly Guid           _patientId;
    private readonly Guid           _providerId;
    private readonly DateTimeOffset _dayStart;
    private readonly DateTimeOffset _dayEnd;

    public ActiveAppointmentByPatientProviderDateSpecification(
        Guid     patientId,
        Guid     providerId,
        DateOnly date)
    {
        _patientId  = patientId;
        _providerId = providerId;
        _dayStart   = new DateTimeOffset(date.Year, date.Month, date.Day, 0,  0,  0,  TimeSpan.Zero);
        _dayEnd     = new DateTimeOffset(date.Year, date.Month, date.Day, 23, 59, 59, TimeSpan.Zero);
    }

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId   == _patientId
          && a.ProviderId  == _providerId
          && a.SlotTime    >= _dayStart
          && a.SlotTime    <= _dayEnd
          && (a.Status == AppointmentStatus.Scheduled
           || a.Status == AppointmentStatus.Booked);

    public List<Expression<Func<Appointment, object>>> Includes => [];
    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Providers/GetProvidersQuery.cs` | New (includes `ProviderDto`) |
| `src/HealthPlatform.Application/Features/Providers/GetProvidersQueryHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/ProvidersBySpecialtySpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommand.cs` | New (includes `BookingConfirmationDto`) |
| `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandValidator.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/PatientProfileByUserIdSpecification.cs` | New |
| `src/HealthPlatform.Application/Features/Appointments/ActiveAppointmentByPatientProviderDateSpecification.cs` | New |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

All 6 existing tests pass. Build succeeds.
