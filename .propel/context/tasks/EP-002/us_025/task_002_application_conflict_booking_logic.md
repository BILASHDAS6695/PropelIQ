# Task 002: Application Layer — Enhanced BookAppointmentCommand with Conflict Logic

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-025 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS command + validator + handler) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 (`IsConflictOverride` fields on entity + `PatientActiveSameDayAppointmentsSpecification`) |

## Objective

Enhance `BookAppointmentCommand` to integrate conflict detection inline, enabling
the booking handler to:

- **Block** hard-conflict bookings for unauthenticated patients (throw `ConflictException → 409`).
- **Allow** staff to bypass hard conflicts by supplying `ForceBook: true` + `OverrideReason`.
- **Warn** on soft conflicts without blocking — the `BookingConfirmationDto` carries a
  `ConflictWarning` field the API controller exposes to the client.
- **Persist** `IsConflictOverride` + `ConflictOverrideReason` on the `Appointment` entity
  so the existing `AuditSaveChangesInterceptor` captures override evidence automatically.

Walk-in registrations (`RegisterWalkInCommand`) are **not modified** — the AC explicitly
states walk-ins do not trigger patient conflict detection (staff discretion).

---

## Acceptance Criteria Covered

- AC: System detects overlap within 30 min before/after proposed slot (hard)
- AC: Soft conflict: warning returned in `BookingConfirmationDto.ConflictWarning`; booking succeeds
- AC: Hard conflict: `ConflictException` (409) returned; conflicting details in the message
- AC: Staff override: `ForceBook = true` + `OverrideReason` bypasses hard block
- AC: Conflict check before slot lock (fail fast — runs before `slot.Status = Booked`)
- AC: Audit log for overrides *(interceptor auto-captures `IsConflictOverride = true`)*

---

## Implementation Steps

### 1. Update `BookAppointmentCommand` and `BookingConfirmationDto`

Edit `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

public sealed record BookAppointmentCommand(
    Guid    SlotId,
    string? VisitReason    = null,
    bool    ForceBook      = false,   // ← new: patient ack (soft) or staff override (hard)
    string? OverrideReason = null)    // ← new: required when ForceBook = true (hard conflict)
    : IRequest<BookingConfirmationDto>;

public sealed record BookingConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset AppointmentTime,
    string         Status,
    string?        ConflictWarning = null);  // ← new: non-null when soft conflict was present
```

---

### 2. Update `BookAppointmentCommandValidator`

Edit `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class BookAppointmentCommandValidator
    : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(c => c.SlotId).NotEmpty();

        // OverrideReason is required when the caller sets ForceBook = true.
        // This covers both staff hard-conflict overrides and soft-conflict
        // acknowledgements that choose to supply a reason.
        RuleFor(c => c.OverrideReason)
            .NotEmpty()
            .WithMessage("An override reason is required when ForceBook is true.")
            .When(c => c.ForceBook);
    }
}
```

---

### 3. Update `BookAppointmentCommandHandler`

Edit `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandHandler.cs`.

Replace the existing duplicate-booking guard (step 3) with the full conflict check,
and persist the override fields.  All other steps are unchanged.

**Replace the existing guard block:**

```csharp
        // ── 3. Duplicate-booking guard ─────────────────────────────────────
        var slotDate  = DateOnly.FromDateTime(slot.StartTime.UtcDateTime);
        var duplicates = await _uow.Repository<Appointment>()
            .GetAsync(
                new ActiveAppointmentByPatientProviderDateSpecification(
                    patient.Id, slot.ProviderId, slotDate), ct);

        if (duplicates.Count > 0)
            throw new ConflictException(
                "You already have an active appointment with this provider on the requested date.");
```

**With the new conflict detection block:**

```csharp
        // ── 3. Cross-provider conflict detection (fail fast, before slot lock) ───
        const int HardWindowMinutes = 30;
        var proposedDate = DateOnly.FromDateTime(slot.StartTime.UtcDateTime);

        var sameDayAppointments = await _uow.Repository<Appointment>()
            .GetAsync(
                new PatientActiveSameDayAppointmentsSpecification(patient.Id, proposedDate),
                ct);

        string? conflictWarning = null;

        if (sameDayAppointments.Count > 0)
        {
            var hardConflict = sameDayAppointments.FirstOrDefault(
                a => Math.Abs((a.SlotTime - slot.StartTime).TotalMinutes) < HardWindowMinutes);

            if (hardConflict is not null)
            {
                if (!command.ForceBook)
                    throw new ConflictException(
                        $"Appointment conflict: you have an overlapping appointment with " +
                        $"{hardConflict.Provider.Name} at {hardConflict.SlotTime:t} UTC " +
                        $"(ID: {hardConflict.Id}). Staff can override with ForceBook = true.");

                // Staff override: warning captured in DTO + persisted on entity
                conflictWarning =
                    $"Override: conflicting appointment {hardConflict.Id} with " +
                    $"{hardConflict.Provider.Name} at {hardConflict.SlotTime:t} UTC.";
            }
            else
            {
                // Soft conflict — same day but outside 30-min window
                var softConflict = sameDayAppointments[0];
                conflictWarning =
                    $"Note: you have another appointment with {softConflict.Provider.Name} at " +
                    $"{softConflict.SlotTime:t} UTC on the same day.";
            }
        }
```

**Update the `Appointment` initialiser (step 5) to persist override fields:**

```csharp
        var appointment = new Appointment
        {
            Id                    = Guid.NewGuid(),
            PatientId             = patient.Id,
            ProviderId            = slot.ProviderId,
            SlotId                = slot.Id,
            SlotTime              = slot.StartTime,
            Status                = AppointmentStatus.Scheduled,
            VisitReason           = command.VisitReason,
            IsWalkIn              = false,
            IsConflictOverride    = command.ForceBook && conflictWarning is not null,  // ← new
            ConflictOverrideReason = command.OverrideReason                           // ← new
        };
```

**Update the return statement to include `ConflictWarning`:**

```csharp
        return new BookingConfirmationDto(
            appointment.Id,
            provider.Id,
            provider.Name,
            appointment.SlotTime,
            appointment.Status.ToString(),
            conflictWarning);   // ← new
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

**Files updated (3 files, no new files):**
- `BookAppointmentCommand.cs` — `ForceBook`, `OverrideReason`, `ConflictWarning`
- `BookAppointmentCommandValidator.cs` — `OverrideReason.NotEmpty()` when `ForceBook`
- `BookAppointmentCommandHandler.cs` — inline conflict check replaces duplicate guard
