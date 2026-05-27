# Task 003: Infrastructure Hangfire Job + API Wiring (SignalR + Endpoints)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-026 |
| **Epic** | EP-002 |
| **Layer** | Infrastructure (Hangfire job) + API (controllers + SignalR + notification models) |
| **Priority** | High |
| **Estimated Effort** | 75 minutes |
| **Dependencies** | Task 001 (Hangfire installed), Task 002 (MarkNoShowCommand, GetNoShowReportQuery) |

## Objective

Four deliverables that complete the no-show tracking feature end-to-end:

1. **`NoShowAutoMarkJob`** — Hangfire recurring job that queries appointments
   whose slot started ≥ 30 min ago and marks each one as NoShow via
   `MarkNoShowCommand(IsAutomatic: true)`.  Uses `IServiceScopeFactory`
   since MediatR and `IUnitOfWork` are scoped services.

2. **Register the recurring job** — in `Program.cs` at startup, schedule
   the job to run every minute (`Cron.Minutely()`).

3. **API endpoints** in `AppointmentsController`:
   - `POST /api/appointments/{id}/no-show` — Staff marks manually
   - `PATCH /api/appointments/{id}/status` — already exists; now accepts
     `"Arrived"` to cover the NoShow → Arrived override (no new endpoint
     needed — just the validator/handler changes from Task 002)

4. **`AdminReportsController`** — new controller at
   `GET /api/admin/reports/no-shows` for the Admin no-show report.

5. **`NotificationModels.cs`** — add `AppointmentNoShowPayload` so the
   provider's queue dashboard can react in real time when a patient no-shows
   and their slot is freed.

---

## Acceptance Criteria Covered

- AC: Hangfire job runs 30 min after slot end: auto-marks unchecked-in appointments as NoShow
- AC: Admin report: no-show rate by provider, by day of week, by time slot
- AC: Staff can mark appointment as NoShow (manual endpoint)
- AC: No-show appointment frees slot only after marking *(handled in MarkNoShowCommandHandler)*
- AC: Audit log entry *(auto via AuditSaveChangesInterceptor)*
- EC: Patient arrives after auto-marking → staff override via existing PATCH endpoint

---

## Implementation Steps

### 1. Create `NoShowAutoMarkJob`

Create new file:
`src/HealthPlatform.Infrastructure/Jobs/NoShowAutoMarkJob.cs`

```csharp
using HealthPlatform.Application.Features.Appointments;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that automatically marks unchecked-in appointments
/// as NoShow after the 30-minute post-slot grace period has elapsed.
///
/// Runs on a minutely schedule.  For each eligible appointment it dispatches
/// <see cref="MarkNoShowCommand"/> with <c>IsAutomatic = true</c>, which
/// frees the slot, increments the patient's no-show counter, and sends the
/// follow-up email.  Failures for individual appointments are caught and
/// logged so a single bad record does not abort the entire batch.
/// </summary>
public sealed class NoShowAutoMarkJob
{
    private readonly IServiceScopeFactory         _scopeFactory;
    private readonly ILogger<NoShowAutoMarkJob>   _logger;

    // 30-minute grace period after slot start before auto-marking.
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

    public NoShowAutoMarkJob(
        IServiceScopeFactory       scopeFactory,
        ILogger<NoShowAutoMarkJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire.  Discovers all eligible appointments
    /// and dispatches <see cref="MarkNoShowCommand"/> for each one.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var uow    = scope.ServiceProvider.GetRequiredService<HealthPlatform.Application.Interfaces.IUnitOfWork>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var cutoff = DateTimeOffset.UtcNow.Subtract(GracePeriod);

        var eligible = await uow.Repository<HealthPlatform.Domain.Entities.Appointment>()
            .GetAsync(new ActiveUnattendedPastCutoffSpecification(cutoff), ct);

        if (eligible.Count == 0)
            return;

        _logger.LogInformation(
            "NoShowAutoMarkJob: found {Count} appointment(s) eligible for auto no-show marking.",
            eligible.Count);

        foreach (var appointment in eligible)
        {
            try
            {
                await sender.Send(
                    new MarkNoShowCommand(appointment.Id, IsAutomatic: true), ct);

                _logger.LogInformation(
                    "NoShowAutoMarkJob: appointment {AppointmentId} marked NoShow (auto).",
                    appointment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "NoShowAutoMarkJob: failed to mark appointment {AppointmentId} as NoShow.",
                    appointment.Id);
            }
        }
    }
}
```

---

### 2. Register `NoShowAutoMarkJob` in Infrastructure `DependencyInjection.cs`

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add the using directive at the top:

```csharp
using HealthPlatform.Infrastructure.Jobs;
```

After the `services.AddHangfireServer();` line, add:

```csharp
        services.AddTransient<NoShowAutoMarkJob>();
```

---

### 3. Register the recurring Hangfire job in `Program.cs`

Edit `src/HealthPlatform.Api/Program.cs`.

Add the using directive at the top if not already present:

```csharp
using Hangfire;
using HealthPlatform.Infrastructure.Jobs;
```

After `app.UseHangfireDashboard(...)` and before `app.Run()`, add:

```csharp
RecurringJob.AddOrUpdate<NoShowAutoMarkJob>(
    recurringJobId: "auto-no-show-mark",
    methodCall:     job => job.ExecuteAsync(CancellationToken.None),
    cronExpression: Cron.Minutely());
```

---

### 4. Add `AppointmentNoShowPayload` to `NotificationModels.cs`

Edit `src/HealthPlatform.Api/Hubs/NotificationModels.cs`.

Append after the last record:

```csharp
/// <summary>
/// Broadcast to a provider's SignalR group when a patient is marked as
/// NoShow (manually by staff or automatically by the Hangfire job).
/// The <see cref="IsAutomatic"/> flag lets the UI differentiate the source.
/// The slot has already been freed at this point.
/// </summary>
public sealed record AppointmentNoShowPayload(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    DateTimeOffset SlotTime,
    bool           IsAutomatic,
    int            PatientTotalNoShowCount);
```

---

### 5. Add `POST /{id}/no-show` to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Add the following action.  Place it near the other status-mutation actions
(after the `Arrive` action, for example):

```csharp
    /// <summary>
    /// Marks an appointment as NoShow (staff action).
    /// The associated slot is freed immediately.
    /// A follow-up email is sent to the patient.
    /// </summary>
    /// <param name="id">Appointment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — NoShow confirmed.<br/>
    /// 400 Bad Request — appointment is already in a terminal or in-progress state.<br/>
    /// 401 Unauthorized — caller is not authenticated.<br/>
    /// 403 Forbidden — caller does not have Staff or Admin role.<br/>
    /// 404 Not Found — appointment does not exist.
    /// </returns>
    [HttpPost("{id:guid}/no-show")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(NoShowConfirmationDto),   StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNoShow(Guid id, CancellationToken ct)
    {
        var result = await _sender.Send(new MarkNoShowCommand(id, IsAutomatic: false), ct);

        await _hub.Clients
            .Group($"provider-{result.ProviderId}")
            .SendAsync("AppointmentNoShow", new AppointmentNoShowPayload(
                AppointmentId:           result.AppointmentId,
                ProviderId:              result.ProviderId,
                PatientId:               result.PatientId,
                SlotTime:                result.SlotTime,
                IsAutomatic:             false,
                PatientTotalNoShowCount: result.PatientTotalNoShowCount), ct);

        return Ok(result);
    }
```

---

### 6. Create `AdminReportsController`

Create new file:
`src/HealthPlatform.Api/Controllers/AdminReportsController.cs`

```csharp
using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Appointments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Admin-only analytics report endpoints.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = PolicyNames.Admin)]
public sealed class AdminReportsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminReportsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns the no-show analytics report for a given date range.
    /// Aggregates by provider, day of week, and time slot.
    /// </summary>
    /// <param name="dateFrom">Start date (inclusive, YYYY-MM-DD).</param>
    /// <param name="dateTo">End date (inclusive, YYYY-MM-DD).</param>
    /// <param name="providerId">Optional provider filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — report data.<br/>
    /// 422 Unprocessable Entity — invalid date range (e.g. exceeds 90 days).<br/>
    /// 401 Unauthorized — caller is not authenticated.<br/>
    /// 403 Forbidden — caller does not have Admin role.
    /// </returns>
    [HttpGet("no-shows")]
    [ProducesResponseType(typeof(NoShowReportDto),          StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetNoShowReport(
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        [FromQuery] Guid?    providerId,
        CancellationToken    ct)
    {
        var report = await _sender.Send(
            new GetNoShowReportQuery(dateFrom, dateTo, providerId), ct);

        return Ok(report);
    }
}
```

---

## Files Modified / Created

| Path | Action |
|------|--------|
| `src/HealthPlatform.Infrastructure/Jobs/NoShowAutoMarkJob.cs` | Create |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Edit — register `NoShowAutoMarkJob` |
| `src/HealthPlatform.Api/Program.cs` | Edit — `RecurringJob.AddOrUpdate` + dashboard wiring |
| `src/HealthPlatform.Api/Hubs/NotificationModels.cs` | Edit — add `AppointmentNoShowPayload` |
| `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` | Edit — add `MarkNoShow` action |
| `src/HealthPlatform.Api/Controllers/AdminReportsController.cs` | Create |

## Verification

- [ ] `dotnet build src/HealthPlatform.sln` passes with no errors
- [ ] `POST /api/appointments/{id}/no-show` with a Staff JWT returns 200 and `AppointmentNoShow` SignalR event is broadcast to `provider-{id}` group
- [ ] `POST /api/appointments/{id}/no-show` with a Patient JWT returns 403
- [ ] `GET /api/admin/reports/no-shows?dateFrom=2025-01-01&dateTo=2025-01-31` returns 200 with aggregated buckets
- [ ] `GET /api/admin/reports/no-shows?dateFrom=2025-01-01&dateTo=2025-06-01` returns 422 (> 90 days)
- [ ] Hangfire dashboard at `/hangfire` shows a `auto-no-show-mark` recurring job
- [ ] After the Hangfire job fires, a Scheduled/Booked appointment whose `SlotTime` is > 30 min in the past transitions to `NoShow` and its slot is freed
- [ ] Audit log contains an entry for each auto or manual no-show marking
- [ ] `PATCH /api/appointments/{id}/status` with `{ "newStatus": "Arrived" }` on a NoShow appointment returns 200 and sets `ArrivalTime`

## Branch & PR Guidance

```bash
git checkout -b feat/us-026-noshow-tracking
```

**PR Title:** `feat(us-026): No-Show Tracking & Follow-Up`

**PR Description:**

```markdown
## Summary
Implements US-026 — No-Show Tracking & Follow-Up for EP-002.

## Changes
- **Domain**: `PatientProfile.TotalNoShowCount` (int, default 0) + EF migration
- **Infrastructure**: Hangfire registered with PostgreSQL storage; `NoShowAutoMarkJob` runs every minute to auto-mark unchecked appointments after 30-min grace period
- **Application**:
  - `MarkNoShowCommand` — marks NoShow, frees slot, increments counter, sends follow-up email
  - `GetNoShowReportQuery` — aggregated no-show analytics by provider / day-of-week / time slot (≤ 90-day window)
  - `UpdateAppointmentStatusCommand` extended: `NoShow → Arrived` override for late patient arrivals
- **API**:
  - `POST /api/appointments/{id}/no-show` (Staff) — manual no-show marking
  - `GET /api/admin/reports/no-shows` (Admin) — no-show analytics report
  - SignalR: `AppointmentNoShow` event broadcast to `provider-{id}` group on marking
  - Hangfire dashboard at `/hangfire` (Admin-only)

## Acceptance Criteria
- [x] Staff manual no-show endpoint
- [x] Hangfire auto-mark job (30-min grace period)
- [x] Follow-up email on no-show
- [x] `TotalNoShowCount` tracked on patient profile
- [x] Admin report by provider / day / time slot
- [x] Slot freed only after marking
- [x] Audit log via AuditSaveChangesInterceptor
- [x] NoShow → Arrived override via existing PATCH endpoint

## Testing
- Unit tests for `MarkNoShowCommandHandler` (happy path + guard cases)
- Unit tests for `GetNoShowReportQueryHandler` (empty range, filtered by provider)
```
