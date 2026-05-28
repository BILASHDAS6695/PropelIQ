# Task 001: Backend — Appointment Intake Status Enrichment & Availability Window

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-043 |
| **Epic** | EP-006 |
| **Layer** | .NET — Domain, Application (CQRS), API |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-042 complete — `IntakeRecord` entity, `IntakeStatus` enum, `IntakeRecordConfiguration` with JSONB all exist |

## Objective

1. **Add `IntakeWindowService`** — static helper encoding the business rule: intake is open from `SlotTime − 7 days` through `ArrivalTime + 15 min` (fallback: `SlotTime + 1 hr` if no check-in yet)
2. **Extend `PatientAppointmentDto`** — add `IntakeStatus: string?` and `IsIntakeWindowOpen: bool`
3. **Update `GetMyAppointmentsQueryHandler`** — include `IntakeRecord` via navigation, project the two new fields
4. **Extend `TodayAppointmentItemDto` + `TodayAppointmentsSearchQuery`** — add `IntakeStatus: string?` and `HasIntakePending: bool?` filter
5. **Update `TodayAppointmentsSearchQueryHandler`** — project intake status; apply optional `HasIntakePending` filter
6. **Add `GET /appointments/{id}/intake-window` endpoint** — returns `{ isOpen: bool, reason: string | null }`

---

## Acceptance Criteria Covered

- AC: Appointment detail shows intake status: Not Started, In Progress, Completed
- AC: Intake available 7 days before appointment through 15 minutes after check-in
- AC: Staff can see which patients have/haven't completed intake for the day
- AC: Staff dashboard filter: "Intake Pending" for today's appointments
- AC: Intake link accessed after appointment completed → "Intake period has ended" message

---

## Implementation Steps

### 1. Create `IntakeWindowService`

Create `src/HealthPlatform.Application/Features/Intake/IntakeWindowService.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Encodes the intake availability window business rule.
/// Open: SlotTime − 7 days  ≤  now  ≤  ArrivalTime + 15 min  (or SlotTime + 1 hr when not yet arrived).
/// Closed after appointment reaches a terminal status.
/// </summary>
public static class IntakeWindowService
{
    private static readonly TimeSpan PreWindowDays    = TimeSpan.FromDays(7);
    private static readonly TimeSpan PostArrivalMins  = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan PostSlotFallback = TimeSpan.FromHours(1);

    public static (bool IsOpen, string? Reason) Evaluate(Appointment appointment)
    {
        var now = DateTimeOffset.UtcNow;

        // Terminal appointment states close the window
        if (appointment.Status is AppointmentStatus.Completed
                                or AppointmentStatus.Cancelled
                                or AppointmentStatus.NoShow)
            return (false, "Intake period has ended.");

        // Intake already completed — no need to reopen
        if (appointment.IntakeRecord?.Status is IntakeStatus.Completed
                                             or IntakeStatus.ReviewedByProvider)
            return (false, "Intake already completed.");

        var windowStart = appointment.SlotTime - PreWindowDays;
        if (now < windowStart)
            return (false, $"Intake opens {windowStart:MMM d, yyyy}.");

        var windowEnd = appointment.ArrivalTime.HasValue
            ? appointment.ArrivalTime.Value + PostArrivalMins
            : appointment.SlotTime + PostSlotFallback;

        if (now > windowEnd)
            return (false, "Intake period has ended.");

        return (true, null);
    }
}
```

### 2. Extend `PatientAppointmentDto`

In `src/HealthPlatform.Application/Features/Appointments/GetMyAppointmentsQuery.cs`, add two new fields to the record:

```csharp
public sealed record PatientAppointmentDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset SlotTime,
    DateTimeOffset EndTime,
    string         Status,
    string?        VisitReason,
    string         PatientName,
    string?        IntakeStatus,       // null = no record yet ("Not Started")
    bool           IsIntakeWindowOpen);
```

### 3. Update `AppointmentsByPatientIdSpecification`

Open `src/HealthPlatform.Application/Features/Appointments/AppointmentsByPatientIdSpecification.cs` and add `.Include(a => a.IntakeRecord)` so the navigation property is populated.

### 4. Update `GetMyAppointmentsQueryHandler`

In the `.Select(a => new PatientAppointmentDto(...))` projection, add:

```csharp
IntakeStatus:        a.IntakeRecord?.Status.ToString(),
IsIntakeWindowOpen:  IntakeWindowService.Evaluate(a).IsOpen,
```

### 5. Extend `TodayAppointmentItemDto` and `TodayAppointmentsSearchQuery`

In `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchQuery.cs`:

```csharp
public sealed record TodayAppointmentsSearchQuery(
    Guid?   ProviderId,
    string? PatientNameFragment,
    Guid?   AppointmentId,
    bool?   HasIntakePending)          // new — null = no filter
    : IRequest<IReadOnlyList<TodayAppointmentItemDto>>;

public sealed record TodayAppointmentItemDto(
    Guid            AppointmentId,
    Guid            PatientId,
    string          PatientFullName,
    string          Status,
    DateTimeOffset  SlotTime,
    bool            IsWalkIn,
    bool            IsLateArrival,
    DateTimeOffset? ArrivalTime,
    string?         IntakeStatus);     // new
```

### 6. Update `TodayAppointmentsSearchSpecification`

Open `src/HealthPlatform.Application/Features/Appointments/TodayAppointmentsSearchSpecification.cs`:

- Add `.Include(a => a.IntakeRecord)` to the specification's `Includes` list.

### 7. Update `TodayAppointmentsSearchQueryHandler`

In the `.Select(...)` projection, add `IntakeStatus: a.IntakeRecord?.Status.ToString()`.

Apply the optional filter after the DB call:

```csharp
var list = appointments
    .Select(a => new TodayAppointmentItemDto(
        ...
        IntakeStatus: a.IntakeRecord?.Status.ToString()))
    .ToList();

if (query.HasIntakePending == true)
    list = list.Where(x => x.IntakeStatus is null or "Draft").ToList();

return list.AsReadOnly();
```

### 8. Update `TodayAppointmentsSearchQueryValidator`

Open the validator and add a no-op rule confirming the pattern compiles (no new validation needed beyond existing).

### 9. Add `IntakeWindowController` endpoint

In `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`, add:

```csharp
[HttpGet("{id:guid}/intake-window")]
[AllowAnonymous]   // window check is unauthenticated-safe; appointment lookup guards data
public async Task<IActionResult> GetIntakeWindow(Guid id, CancellationToken ct)
{
    var results = await _uow.Repository<Appointment>()
        .GetAsync(new AppointmentWithIntakeSpecification(id), ct);
    if (results.Count == 0)
        return NotFound();

    var (isOpen, reason) = IntakeWindowService.Evaluate(results[0]);
    return Ok(new { isOpen, reason });
}
```

Create `src/HealthPlatform.Application/Features/Appointments/AppointmentWithIntakeSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class AppointmentWithIntakeSpecification : ISpecification<Appointment>
{
    public AppointmentWithIntakeSpecification(Guid appointmentId)
    {
        Criteria = a => a.Id == appointmentId;
        Includes.Add(a => a.IntakeRecord!);
        Includes.Add(a => a.Patient);
        Includes.Add(a => a.Slot!);
        IsPagingEnabled = true;
        Skip = 0;
        Take = 1;
    }

    public Expression<Func<Appointment, bool>>? Criteria { get; }
    public List<Expression<Func<Appointment, object>>> Includes { get; } = [];
    public Expression<Func<Appointment, object>>? OrderBy { get; }
    public Expression<Func<Appointment, object>>? OrderByDescending { get; }
    public int Skip { get; }
    public int Take { get; }
    public bool IsPagingEnabled { get; }
}
```

---

## Tests

No new test file needed for this task — existing `TodayAppointmentsSearchQueryHandler` tests must still compile after the new `HasIntakePending` parameter is added. Update any existing test instantiations of `TodayAppointmentsSearchQuery(...)` to pass `null` as the fourth argument.

**Verification:** `dotnet test` → all tests pass (58 baseline).
