# Task 002: AppointmentReminderJob, HangfireReminderScheduler, and Handler Integrations

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-033 |
| **Epic** | EP-004 |
| **Layer** | Infrastructure (job + scheduler) + Application (handler integrations) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 — `IReminderScheduler`, `ReminderSettings`, `Appointment.Reminder24hJobId/2hJobId` |

## Objective

Deliver the full runtime behaviour of the reminder service:

1. **`AppointmentForReminderSpecification`** — eager-loads Patient, Patient.User,
   and Provider in a single query; used by the job to avoid N+1.
2. **`AppointmentReminderJob`** — Hangfire job that checks appointment status at
   execution time (idempotent) and sends the reminder email via `IEmailSender`.
3. **`HangfireReminderScheduler`** — `IReminderScheduler` implementation that
   calculates trigger times, skips past-due jobs, and stores returned job IDs.
4. **DI wiring** — bind `ReminderSettings` and register
   `HangfireReminderScheduler` as `IReminderScheduler`.
5. **Handler integrations** — wire `IReminderScheduler` into the Book, Cancel,
   and Reschedule command handlers.

---

## Acceptance Criteria Covered

- AC: When appointment is booked, two Hangfire jobs scheduled: 24 h before and 2 h before
- AC: Reminder email includes: provider name, date, time, "Cancel" link placeholder
- AC: If appointment cancelled before reminder fires → job removed from queue
- AC: If appointment rescheduled → old reminder jobs deleted, new ones scheduled
- AC: Reminder not sent if appointment already completed or no-show
- AC: Reminder job idempotent (safe to execute multiple times)
- AC: Appointment booked < 2 h from now → both jobs skipped
- AC: Appointment booked < 24 h from now → 24 h job skipped, 2 h job scheduled

---

## Implementation Steps

### 1. Create `AppointmentForReminderSpecification`

Create `src/HealthPlatform.Application/Features/Appointments/AppointmentForReminderSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Loads a single appointment by ID with the navigations required by
/// <see cref="HealthPlatform.Infrastructure.Reminders.AppointmentReminderJob"/>:
/// Patient, Patient.User (for email address), and Provider (for name).
/// </summary>
internal sealed class AppointmentForReminderSpecification : ISpecification<Appointment>
{
    private readonly Guid _appointmentId;

    public AppointmentForReminderSpecification(Guid appointmentId)
        => _appointmentId = appointmentId;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.Id == _appointmentId;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Patient,
        a => a.Patient.User,
        a => a.Provider,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
```

### 2. Create `AppointmentReminderJob`

Create `src/HealthPlatform.Infrastructure/Reminders/AppointmentReminderJob.cs`:

```csharp
using Hangfire;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Hangfire job that sends an appointment reminder email.
/// Idempotent: if the appointment is already Cancelled, Completed, or NoShow
/// at execution time the job logs and exits without sending.
/// Retried up to 3 times with exponential back-off on transient failures.
/// </summary>
internal sealed class AppointmentReminderJob
{
    private readonly IUnitOfWork                    _uow;
    private readonly IEmailSender                   _emailSender;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(
        IUnitOfWork                    uow,
        IEmailSender                   emailSender,
        ILogger<AppointmentReminderJob> logger)
    {
        _uow         = uow;
        _emailSender = emailSender;
        _logger      = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 1500, 7500 })]
    public async Task ExecuteAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentForReminderSpecification(appointmentId), ct);

        if (results.Count == 0)
        {
            _logger.LogWarning(
                "AppointmentReminderJob: appointment {AppointmentId} not found — skipping.",
                appointmentId);
            return;
        }

        var appointment = results[0];

        // Idempotency guard — skip terminal states
        if (appointment.Status is AppointmentStatus.Cancelled
                                or AppointmentStatus.Completed
                                or AppointmentStatus.NoShow)
        {
            _logger.LogInformation(
                "AppointmentReminderJob: appointment {AppointmentId} is {Status} — skipping reminder.",
                appointmentId,
                appointment.Status);
            return;
        }

        var patientName  = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}";
        var providerName = appointment.Provider.Name;
        var email        = appointment.Patient.User.Email;

        var (subject, body) = EmailTemplateService.Reminder(
            patientName,
            providerName,
            appointment.SlotTime,
            appointment.Id);

        _logger.LogInformation(
            "AppointmentReminderJob: sending reminder for appointment {AppointmentId} to {Email}.",
            appointmentId,
            email);

        await _emailSender.SendAsync(email, subject, body, ct);
    }
}
```

> **Design note — no `Using` directive for Domain.Entities**: `Appointment` is
> already in scope because the `AppointmentForReminderSpecification` file brings
> in the `HealthPlatform.Domain.Entities` namespace transitively.  Add an explicit
> `using HealthPlatform.Domain.Entities;` if the compiler requires it.

### 3. Create `HangfireReminderScheduler`

Create `src/HealthPlatform.Infrastructure/Reminders/HangfireReminderScheduler.cs`:

```csharp
using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Schedules or cancels Hangfire reminder jobs for a given appointment.
/// Jobs that would fire in the past are silently skipped (e.g. appointment
/// booked &lt; 24 h from now skips the 24 h reminder).
/// </summary>
internal sealed class HangfireReminderScheduler : IReminderScheduler
{
    private readonly IBackgroundJobClient          _jobs;
    private readonly IUnitOfWork                   _uow;
    private readonly ReminderSettings              _settings;
    private readonly ILogger<HangfireReminderScheduler> _logger;

    public HangfireReminderScheduler(
        IBackgroundJobClient          jobs,
        IUnitOfWork                   uow,
        IOptions<ReminderSettings>    settings,
        ILogger<HangfireReminderScheduler> logger)
    {
        _jobs     = jobs;
        _uow      = uow;
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task ScheduleAsync(Appointment appointment, CancellationToken ct = default)
    {
        var now      = DateTimeOffset.UtcNow;
        var slotTime = appointment.SlotTime;

        // ── 24-hour reminder ──────────────────────────────────────────────
        var trigger24h = slotTime.AddHours(-_settings.HoursBeforeFirst);
        if (trigger24h > now)
        {
            appointment.Reminder24hJobId = _jobs.Schedule<AppointmentReminderJob>(
                job => job.ExecuteAsync(appointment.Id, CancellationToken.None),
                trigger24h);

            _logger.LogInformation(
                "Reminder [{HoursBeforeFirst}h] scheduled for appointment {AppointmentId} at {TriggerTime}.",
                _settings.HoursBeforeFirst,
                appointment.Id,
                trigger24h);
        }
        else
        {
            _logger.LogDebug(
                "Reminder [{HoursBeforeFirst}h] skipped for appointment {AppointmentId} — trigger time {TriggerTime} is in the past.",
                _settings.HoursBeforeFirst,
                appointment.Id,
                trigger24h);
        }

        // ── 2-hour reminder ───────────────────────────────────────────────
        var trigger2h = slotTime.AddHours(-_settings.HoursBeforeSecond);
        if (trigger2h > now)
        {
            appointment.Reminder2hJobId = _jobs.Schedule<AppointmentReminderJob>(
                job => job.ExecuteAsync(appointment.Id, CancellationToken.None),
                trigger2h);

            _logger.LogInformation(
                "Reminder [{HoursBeforeSecond}h] scheduled for appointment {AppointmentId} at {TriggerTime}.",
                _settings.HoursBeforeSecond,
                appointment.Id,
                trigger2h);
        }
        else
        {
            _logger.LogDebug(
                "Reminder [{HoursBeforeSecond}h] skipped for appointment {AppointmentId} — trigger time {TriggerTime} is in the past.",
                _settings.HoursBeforeSecond,
                appointment.Id,
                trigger2h);
        }

        _uow.Update(appointment);
        await _uow.SaveChangesAsync(ct);
    }

    public void Cancel(Appointment appointment)
    {
        if (appointment.Reminder24hJobId is not null)
        {
            _jobs.Delete(appointment.Reminder24hJobId);
            appointment.Reminder24hJobId = null;
            _logger.LogInformation(
                "Reminder [24h] job deleted for appointment {AppointmentId}.",
                appointment.Id);
        }

        if (appointment.Reminder2hJobId is not null)
        {
            _jobs.Delete(appointment.Reminder2hJobId);
            appointment.Reminder2hJobId = null;
            _logger.LogInformation(
                "Reminder [2h] job deleted for appointment {AppointmentId}.",
                appointment.Id);
        }
    }
}
```

### 4. Update `DependencyInjection.cs`

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`, add after the
`services.Configure<SmtpSettings>(…)` line:

```csharp
services.AddTransient<AppointmentReminderJob>();
services.AddScoped<IReminderScheduler, HangfireReminderScheduler>();

services.Configure<ReminderSettings>(
    configuration.GetSection(ReminderSettings.SectionName));
```

Add usings at the top of the file:

```csharp
using HealthPlatform.Infrastructure.Reminders;
```

### 5. Integrate into `BookAppointmentCommandHandler`

**Step 5a** — Add `IReminderScheduler` to the constructor:

```csharp
private readonly IUnitOfWork         _uow;
private readonly ICurrentUserService _currentUser;
private readonly IEmailSender        _emailSender;
private readonly IReminderScheduler  _reminders;

public BookAppointmentCommandHandler(
    IUnitOfWork         uow,
    ICurrentUserService currentUser,
    IEmailSender        emailSender,
    IReminderScheduler  reminders)
{
    _uow         = uow;
    _currentUser = currentUser;
    _emailSender = emailSender;
    _reminders   = reminders;
}
```

**Step 5b** — After sending the confirmation email (the existing `await _emailSender.SendAsync(…)` block), append:

```csharp
// Schedule 24h + 2h reminder jobs (skips past-due triggers automatically)
await _reminders.ScheduleAsync(appointment, ct);
```

> The `ScheduleAsync` call persists the Hangfire job IDs back to the DB via its
> own `SaveChangesAsync`.  The appointment entity is still tracked by the same
> scoped DbContext, so EF picks up the job-ID updates.

### 6. Integrate into `CancelAppointmentCommandHandler`

**Step 6a** — Add `IReminderScheduler` to the constructor (same pattern as step 5a).

**Step 6b** — In the `Handle` method, call `Cancel` **before** the existing
`await _uow.SaveChangesAsync(ct)` so the job-ID nullifications are committed in
the same DB round-trip as the status change.  Find the line just before `SaveChanges`:

```csharp
// Delete any pending reminder jobs before saving the cancellation
_reminders.Cancel(appointment);

await _uow.SaveChangesAsync(ct);
```

### 7. Integrate into `RescheduleAppointmentCommandHandler`

**Step 7a** — Add `IReminderScheduler` to the constructor.

**Step 7b** — Cancel reminders for the *old* appointment (just before the
existing `await _uow.SaveChangesAsync(ct)`):

```csharp
// Cancel pending reminders for the old appointment slot
_reminders.Cancel(existing);

await _uow.SaveChangesAsync(ct);
```

**Step 7c** — Schedule reminders for the *new* appointment (after the existing
save, alongside or after the reschedule confirmation email):

```csharp
// Schedule new reminders for the rescheduled appointment
await _reminders.ScheduleAsync(newAppointment, ct);
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Appointments/AppointmentForReminderSpecification.cs` | New |
| `src/HealthPlatform.Infrastructure/Reminders/AppointmentReminderJob.cs` | New |
| `src/HealthPlatform.Infrastructure/Reminders/HangfireReminderScheduler.cs` | New |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register job, scheduler, settings |
| `src/HealthPlatform.Application/Features/Appointments/BookAppointmentCommandHandler.cs` | Add `IReminderScheduler` injection + `ScheduleAsync` call |
| `src/HealthPlatform.Application/Features/Appointments/CancelAppointmentCommandHandler.cs` | Add `IReminderScheduler` injection + `Cancel` call |
| `src/HealthPlatform.Application/Features/Appointments/RescheduleAppointmentCommandHandler.cs` | Add `IReminderScheduler` injection + `Cancel` + `ScheduleAsync` calls |

---

## Verification

```bash
dotnet build src/HealthPlatform.sln --configuration Release
# Expect: 0 errors

dotnet test src/HealthPlatform.sln --configuration Release
# Expect: all existing tests pass (handlers now require IReminderScheduler
#         in DI — existing test stubs must provide a NoOpReminderScheduler)
```

> **Note on existing tests**: `BookAppointmentCommandTests` and
> `CancelAppointmentCommandTests`/`RescheduleAppointmentCommandTests` build their
> mediator with `services.AddApplication()` + explicit service registrations.
> Those tests will fail to resolve `IReminderScheduler` once the handlers inject
> it.  Add `services.AddScoped<IReminderScheduler>(_ => new NoOpReminderScheduler())`
> to each test's builder, where `NoOpReminderScheduler` is defined alongside the
> other test doubles:
>
> ```csharp
> internal sealed class NoOpReminderScheduler : IReminderScheduler
> {
>     public Task ScheduleAsync(Appointment appointment, CancellationToken ct = default)
>         => Task.CompletedTask;
>     public void Cancel(Appointment appointment) { }
> }
> ```
