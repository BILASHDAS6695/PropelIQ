# Task 002: Backend — Reminder Email Intake Link & Walk-in Intake Trigger

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-043 |
| **Epic** | EP-006 |
| **Layer** | .NET — Infrastructure (Reminders, Messaging), Application (CQRS), API |
| **Priority** | High |
| **Estimated Effort** | 25 minutes |
| **Dependencies** | Task 001 complete — `IntakeWindowService` exists; `AppointmentReminderJob` exists with `EmailTemplateService.Reminder()` |

## Objective

1. **Update `EmailTemplateService.Reminder()`** — add an `intakeUrl` parameter; when non-null, append a "Complete Your Intake" CTA button to the reminder email body
2. **Update `AppointmentReminderJob`** — construct the intake URL and pass it to the updated template (only for the 24-hour job and non-walk-in appointments whose intake window is open)
3. **Add `TriggerWalkInIntakeCommand`** — creates a blank `Draft` `IntakeRecord` for a walk-in patient so staff can initiate intake from the check-in screen
4. **Add `POST /appointments/{id}/intake/trigger` API endpoint** — staff-only, wires to the new command

---

## Acceptance Criteria Covered

- AC: Intake link included in 24-hour reminder email
- AC: Walk-in patient → staff can trigger intake from check-in flow
- AC: Appointment rescheduled → intake retains link to new appointment (no change needed — IntakeRecord links via AppointmentId; rescheduling creates new appointment and references same patient; handled by SaveIntakeDraftCommand in US-042)

---

## Implementation Steps

### 1. Update `EmailTemplateService.Reminder()`

Open `src/HealthPlatform.Infrastructure/Messaging/EmailTemplateService.cs`.

Change the `Reminder` method signature to accept an optional intake URL:

```csharp
internal static (string subject, string body) Reminder(
    string         patientName,
    string         providerName,
    DateTimeOffset slotTime,
    Guid           appointmentId,
    string?        intakeUrl = null)     // new optional param
```

Inside the method body, append a CTA block when `intakeUrl` is not null:

```csharp
var intakeCta = intakeUrl is not null
    ? $"""
      <tr><td style="padding:16px 0;">
        <p style="font-size:14px;color:#333;margin:0 0 8px;">
          Save time at your appointment — complete your intake form online:
        </p>
        <a href="{intakeUrl}"
           style="display:inline-block;background:#1976d2;color:#ffffff;padding:10px 20px;
                  border-radius:4px;text-decoration:none;font-size:14px;">
          Complete Your Intake
        </a>
      </td></tr>
      """
    : string.Empty;
```

Insert `{intakeCta}` into the detail table rows section.

### 2. Update `AppointmentReminderJob`

Open `src/HealthPlatform.Infrastructure/Reminders/AppointmentReminderJob.cs`.

Inject `IConfiguration` or `ReminderSettings` to obtain the frontend base URL (e.g. `https://app.healthplatform.com`). Add a constructor parameter:

```csharp
private readonly ReminderSettings _settings;
// add to constructor and assignment
```

In `ExecuteAsync`, before calling `EmailTemplateService.Reminder(...)`, compute `intakeUrl`:

```csharp
string? intakeUrl = null;
if (!appointment.IsWalkIn)
{
    var (isOpen, _) = IntakeWindowService.Evaluate(appointment);
    if (isOpen)
        intakeUrl = $"{_settings.FrontendBaseUrl}/intake/form?appointmentId={appointment.Id}";
}
```

Pass `intakeUrl` to `EmailTemplateService.Reminder(...)`.

### 3. Update `ReminderSettings`

Open `src/HealthPlatform.Infrastructure/Reminders/ReminderSettings.cs` and add:

```csharp
/// <summary>Base URL of the Angular frontend, used to build intake deep-links in reminder emails.</summary>
public string FrontendBaseUrl { get; init; } = "https://localhost:4200";
```

Update `appsettings.json` under `"Reminders"`:

```json
"Reminders": {
  "FrontendBaseUrl": "https://localhost:4200"
}
```

### 4. Add `TriggerWalkInIntakeCommand`

Create `src/HealthPlatform.Application/Features/Intake/TriggerWalkInIntakeCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Staff-triggered command: creates a blank Draft IntakeRecord for a walk-in patient
/// so that the patient can complete intake at the clinic kiosk or front-desk tablet.
/// Idempotent: if a Draft already exists for this appointment, returns its ID.
/// </summary>
public record TriggerWalkInIntakeCommand(Guid AppointmentId, Guid StaffUserId) : IRequest<Guid>;
```

Create `src/HealthPlatform.Application/Features/Intake/TriggerWalkInIntakeCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Intake;

internal sealed class TriggerWalkInIntakeCommandHandler
    : IRequestHandler<TriggerWalkInIntakeCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public TriggerWalkInIntakeCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(TriggerWalkInIntakeCommand cmd, CancellationToken ct)
    {
        // Idempotency: return existing Draft if present
        var existing = await _uow.Repository<IntakeRecord>()
            .GetAsync(new IntakeRecordByAppointmentSpecification(cmd.AppointmentId), ct);

        if (existing.Count > 0 && existing[0].Status == IntakeStatus.Draft)
            return existing[0].Id;

        // Look up the appointment to get PatientId
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentWithIntakeSpecification(cmd.AppointmentId), ct);

        if (appointments.Count == 0)
            throw new InvalidOperationException(
                $"Appointment {cmd.AppointmentId} not found.");

        var appt = appointments[0];

        var record = new IntakeRecord
        {
            PatientId     = appt.PatientId,
            AppointmentId = cmd.AppointmentId,
            Mode          = IntakeMode.ManualForm,
            Status        = IntakeStatus.Draft,
        };

        await _uow.Repository<IntakeRecord>().AddAsync(record, ct);
        await _uow.SaveChangesAsync(ct);

        return record.Id;
    }
}
```

### 5. Add API endpoint

In `src/HealthPlatform.Api/Controllers/IntakeController.cs`, add a new endpoint:

```csharp
/// <summary>Staff triggers intake for a walk-in patient.</summary>
[HttpPost("/api/appointments/{appointmentId:guid}/intake/trigger")]
[Authorize(Roles = "Staff,Admin")]
public async Task<IActionResult> TriggerWalkInIntake(
    Guid appointmentId,
    CancellationToken ct)
{
    if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        return Unauthorized();

    var id = await _sender.Send(
        new TriggerWalkInIntakeCommand(appointmentId, _currentUser.UserId.Value), ct);
    return Ok(new { id });
}
```

---

## Tests

Add `src/HealthPlatform.Tests/Application/TriggerWalkInIntakeCommandTests.cs`:

```csharp
using HealthPlatform.Application.Features.Intake;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using NSubstitute;
using Xunit;

namespace HealthPlatform.Tests.Application;

public class TriggerWalkInIntakeCommandTests
{
    [Fact]
    public async Task Handle_WhenNoDraftExists_CreatesNewDraftRecord()
    {
        // arrange — existing query returns no record
        var uow  = Substitute.For<IUnitOfWork>();
        var intakeRepo = Substitute.For<IRepository<IntakeRecord>>();
        var apptRepo   = Substitute.For<IRepository<Appointment>>();

        var appointmentId = Guid.NewGuid();
        var patientId     = Guid.NewGuid();

        intakeRepo.GetAsync(Arg.Any<ISpecification<IntakeRecord>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var appt = new Appointment { Id = appointmentId, PatientId = patientId, IsWalkIn = true };
        apptRepo.GetAsync(Arg.Any<ISpecification<Appointment>>(), Arg.Any<CancellationToken>())
            .Returns([appt]);

        uow.Repository<IntakeRecord>().Returns(intakeRepo);
        uow.Repository<Appointment>().Returns(apptRepo);

        IntakeRecord? saved = null;
        await intakeRepo.AddAsync(Arg.Do<IntakeRecord>(r => saved = r), Arg.Any<CancellationToken>());

        var handler = new TriggerWalkInIntakeCommandHandler(uow);
        var cmd = new TriggerWalkInIntakeCommand(appointmentId, Guid.NewGuid());

        // act
        await handler.Handle(cmd, CancellationToken.None);

        // assert
        Assert.NotNull(saved);
        Assert.Equal(IntakeStatus.Draft, saved!.Status);
        Assert.Equal(patientId, saved.PatientId);
    }
}
```

**Verification:** `dotnet test` → 59 tests pass (58 baseline + 1 new).
