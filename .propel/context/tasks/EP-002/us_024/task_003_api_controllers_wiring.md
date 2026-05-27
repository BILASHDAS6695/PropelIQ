# Task 003: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-024 |
| **Epic** | EP-002 |
| **Layer** | API (controllers, SignalR broadcast, notification models) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 + Task 002 (domain + CQRS handlers registered) |

## Objective

Expose two new endpoints and broadcast a SignalR event whenever a provider
advances an appointment's status.  All actions are Staff/Admin-only.

- `POST /api/appointments/{id}/status` — update appointment status (Arrived → InProgress → Completed); broadcasts `AppointmentStatusChangedPayload` to the provider's dashboard group.
- `GET  /api/providers/{id}/queue/dashboard` — returns sorted queue + summary counts.

The existing `GET /api/providers/{id}/queue` endpoint is **not modified**; the
new `dashboard` route sits alongside it for backward compatibility.

## Acceptance Criteria Covered

- AC: Provider can change appointment status: Arrived → InProgress → Completed
- AC: Real-time updates via SignalR when status changes
- AC: Dashboard shows all appointments for selected date
- AC: Queue count summary at top
- AC: Color coding supported — `status` string drives client-side CSS class

---

## Implementation Steps

### 1. Add `AppointmentStatusChangedPayload` to `NotificationModels.cs`

Edit `src/HealthPlatform.Api/Hubs/NotificationModels.cs`.

Append after `PatientArrivedPayload`:

```csharp
/// <summary>
/// Broadcast to a provider's SignalR group when a provider changes an
/// appointment status (e.g. Arrived → InProgress → Completed).
/// </summary>
public sealed record AppointmentStatusChangedPayload(
    Guid   AppointmentId,
    Guid   ProviderId,
    string OldStatus,
    string NewStatus);
```

---

### 2. Add `UpdateStatus` action to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Add after the `RevertArrival` action:

```csharp
/// <summary>
/// Advances an appointment through the provider-driven status chain:
/// Arrived → InProgress → Completed.
/// Broadcasts a real-time notification to the provider's dashboard group.
/// Staff and Admin only.
/// </summary>
/// <param name="id">The appointment ID.</param>
/// <param name="request">Target status string.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — status update confirmation with old and new status.<br/>
/// 400 Bad Request — invalid transition (e.g. Scheduled → Completed).<br/>
/// 404 Not Found — appointment does not exist.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("{id:guid}/status")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(StatusUpdateConfirmationDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),              StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails),              StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails),    StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> UpdateStatus(
    [FromRoute] Guid               id,
    [FromBody]  UpdateStatusRequest request,
    CancellationToken              ct)
{
    var confirmation = await _sender.Send(
        new UpdateAppointmentStatusCommand(id, request.NewStatus), ct);

    await _hub.Clients
        .Group($"provider-{confirmation.ProviderId}")
        .SendAsync(
            "AppointmentStatusChanged",
            new AppointmentStatusChangedPayload(
                confirmation.AppointmentId,
                confirmation.ProviderId,
                confirmation.OldStatus,
                confirmation.NewStatus),
            ct);

    return Ok(confirmation);
}
```

Add the request record at the bottom of the file alongside the other records:

```csharp
/// <summary>Payload for updating an appointment status.</summary>
public sealed record UpdateStatusRequest(string NewStatus);
```

---

### 3. Add `GetQueueDashboard` action to `ProvidersController`

Edit `src/HealthPlatform.Api/Controllers/ProvidersController.cs`.

Add the `using` for `GetProviderQueueDashboardQuery` at the top if not already present (the file already imports from `HealthPlatform.Application.Features.Providers`).

Add after the existing `GetQueue` action:

```csharp
/// <summary>
/// Returns the provider's daily queue with multi-key sort and a summary
/// count block for the dashboard header ("N waiting, N in progress, N remaining").
/// Default date is today when the <c>date</c> parameter is omitted.
/// </summary>
/// <param name="id">Provider ID.</param>
/// <param name="date">Calendar date in <c>yyyy-MM-dd</c> format (optional, defaults to today).</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — sorted queue items and summary counts.<br/>
/// 400 Bad Request — date parameter is present but invalid.
/// </returns>
[HttpGet("{id:guid}/queue/dashboard")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(QueueDashboardDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),    StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetQueueDashboard(
    Guid               id,
    [FromQuery] string? date,
    CancellationToken  ct)
{
    DateOnly parsedDate;
    if (string.IsNullOrWhiteSpace(date))
    {
        parsedDate = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
    }
    else if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out parsedDate))
    {
        return BadRequest(new ProblemDetails
        {
            Title  = "Invalid date format.",
            Detail = "The 'date' query parameter must be in yyyy-MM-dd format.",
            Status = StatusCodes.Status400BadRequest
        });
    }

    var dashboard = await _sender.Send(
        new GetProviderQueueDashboardQuery(id, parsedDate), ct);
    return Ok(dashboard);
}
```

---

## API Surface Summary

| Method | Route | Policy | Responses |
|--------|-------|--------|-----------|
| `POST` | `/api/appointments/{id}/status` | Staff | 200, 400, 404, 422 |
| `GET`  | `/api/providers/{id}/queue/dashboard` | Staff | 200, 400 |

---

## SignalR Client Contract

```javascript
// Provider dashboard subscribes on load
connection.invoke("SubscribeToProvider", providerId);

// Status change event (e.g. Arrived → InProgress)
connection.on("AppointmentStatusChanged", (payload) => {
  // payload: { appointmentId, providerId, oldStatus, newStatus }
  updateQueueEntry(payload.appointmentId, payload.newStatus);
  applyColorClass(payload.appointmentId, payload.newStatus);
  // newStatus values: "InProgress" (green), "Completed" (hide/archive)
});
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

Open Swagger UI at `http://localhost:5013/swagger`:
- `POST /api/appointments/{id}/status` — visible, requires Staff JWT
- `GET  /api/providers/{id}/queue/dashboard` — visible, returns `QueueDashboardDto`

Integration smoke test (requires a JWT with `Staff` or `Admin` role):

```bash
# 1. Get dashboard for today
GET /api/providers/{providerId}/queue/dashboard
# → 200 { items: [...], summary: { waiting: N, inProgress: N, remaining: N } }

# 2. Advance Arrived → InProgress
POST /api/appointments/{appointmentId}/status
{ "newStatus": "InProgress" }
# → 200 { appointmentId, providerId, oldStatus: "Arrived", newStatus: "InProgress" }
# → SignalR "AppointmentStatusChanged" delivered to provider-{id} group

# 3. Invalid transition
POST /api/appointments/{appointmentId}/status
{ "newStatus": "InProgress" }  # appointment is already InProgress
# → 400 "Cannot transition from 'InProgress' to 'InProgress'"

# 4. Complete the appointment
POST /api/appointments/{appointmentId}/status
{ "newStatus": "Completed" }
# → 200 { oldStatus: "InProgress", newStatus: "Completed" }
```
