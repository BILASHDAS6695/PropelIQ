# Task 001: Domain Changes, ReminderSettings, and IReminderScheduler Interface

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-033 |
| **Epic** | EP-004 |
| **Layer** | Domain + Application (interface) + Infrastructure (settings + migration) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-032 complete — `IEmailSender`, `HangfireEmailDispatcher`, `EmailTemplateService.Reminder` already exist |

## Objective

Lay the groundwork for scheduled appointment reminders:

1. Add **two nullable job-ID columns** to `Appointment` — so Hangfire job IDs can
   be stored and later deleted on cancellation / reschedule.
2. Create **`ReminderSettings`** — admin-configurable reminder intervals bound
   from `appsettings.json`.
3. Define **`IReminderScheduler`** — the Application-layer contract that handlers
   call to schedule or cancel reminder jobs.
4. Apply an **EF Core migration** to add the new columns to the `appointments`
   table.

---

## Acceptance Criteria Covered

- AC: When appointment is booked, two Hangfire jobs scheduled: 24 h before and 2 h before
- AC: If appointment cancelled before reminder fires → job removed from queue
- AC: If appointment rescheduled → old reminder jobs deleted, new ones scheduled
- AC: Admin configurable reminder intervals (default: 24 h + 2 h)
- AC: Server restart → Hangfire persists jobs, no reminders lost

---

## Implementation Steps

### 1. Extend `Appointment` entity

Edit `src/HealthPlatform.Domain/Entities/Appointment.cs` — add two nullable
properties at the end of the column declarations (before navigation properties):

```csharp
// ── Reminder scheduling ───────────────────────────────────────────────────
/// <summary>Hangfire job ID for the 24-hour-before reminder. Null when not
/// yet scheduled or after the job has been deleted.</summary>
public string? Reminder24hJobId { get; set; }

/// <summary>Hangfire job ID for the 2-hour-before reminder. Null when not
/// yet scheduled or after the job has been deleted.</summary>
public string? Reminder2hJobId { get; set; }
```

> **Placement**: insert immediately before `public PatientProfile Patient …`.

### 2. Create `ReminderSettings`

Create `src/HealthPlatform.Infrastructure/Reminders/ReminderSettings.cs`:

```csharp
namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Admin-configurable reminder intervals bound from the "Reminders" section of
/// appsettings.json.  Both values default to the story's required intervals.
/// </summary>
public sealed class ReminderSettings
{
    public const string SectionName = "Reminders";

    /// <summary>Hours before the appointment at which the first reminder fires (default: 24).</summary>
    public int HoursBeforeFirst  { get; init; } = 24;

    /// <summary>Hours before the appointment at which the second reminder fires (default: 2).</summary>
    public int HoursBeforeSecond { get; init; } = 2;
}
```

### 3. Define `IReminderScheduler` interface

Create `src/HealthPlatform.Application/Interfaces/IReminderScheduler.cs`:

```csharp
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Schedules or cancels Hangfire reminder jobs for a given appointment.
/// Implementations live in the Infrastructure layer and interact with
/// <see cref="Hangfire.IBackgroundJobClient"/> directly.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>
    /// Enqueues the configured reminder jobs (default: 24 h and 2 h before
    /// slot time) for <paramref name="appointment"/>.  Jobs that would fire
    /// in the past are silently skipped.  Persists the returned Hangfire job
    /// IDs back onto the entity and saves via <see cref="IUnitOfWork"/>.
    /// </summary>
    Task ScheduleAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// Deletes any pending reminder jobs from Hangfire and nulls the job-ID
    /// fields on <paramref name="appointment"/>.  Does <em>not</em> call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> — the calling handler is
    /// responsible for the final save so that job-ID nullification is batched
    /// with the status-change mutation.
    /// </summary>
    void Cancel(Appointment appointment);
}
```

### 4. Add configuration to `appsettings.json`

Edit `src/HealthPlatform.Api/appsettings.json` — add after the `"Smtp"` block:

```json
"Reminders": {
  "HoursBeforeFirst": 24,
  "HoursBeforeSecond": 2
}
```

Edit `src/HealthPlatform.Api/appsettings.Development.json` — add after `"Smtp"`:

```json
"Reminders": {
  "HoursBeforeFirst": 24,
  "HoursBeforeSecond": 2
}
```

### 5. Apply EF Core migration

Run from `src/`:

```bash
dotnet ef migrations add AddAppointmentReminderJobIds \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
dotnet ef database update \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

The generated migration should add two nullable `text` columns to the
`appointments` table:

```csharp
migrationBuilder.AddColumn<string>(
    name: "reminder24h_job_id",
    table: "appointments",
    type: "text",
    nullable: true);

migrationBuilder.AddColumn<string>(
    name: "reminder2h_job_id",
    table: "appointments",
    type: "text",
    nullable: true);
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Domain/Entities/Appointment.cs` | Add `Reminder24hJobId?` + `Reminder2hJobId?` |
| `src/HealthPlatform.Infrastructure/Reminders/ReminderSettings.cs` | New — configurable intervals |
| `src/HealthPlatform.Application/Interfaces/IReminderScheduler.cs` | New — scheduler contract |
| `src/HealthPlatform.Api/appsettings.json` | Add `Reminders` section |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `Reminders` section |
| `src/HealthPlatform.Infrastructure/Migrations/…AddAppointmentReminderJobIds.cs` | EF migration |

---

## Verification

```bash
dotnet build src/HealthPlatform.sln --configuration Release
# Expect: 0 errors, 0 warnings
```
