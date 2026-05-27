# Task 002: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-023 |
| **Epic** | EP-002 |
| **Layer** | API (controller actions, SignalR broadcast, request models) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (all CQRS handlers registered via Application DI) |

## Objective

Expose three new staff-only actions on `AppointmentsController` and broadcast a
SignalR notification to the provider's dashboard group whenever a patient is
marked as arrived.  The `PatientArrivedPayload` record is added to the existing
`NotificationModels.cs` file so the hub model stays co-located.

Staff restriction is enforced at the controller level via
`[Authorize(Policy = PolicyNames.Staff)]`.  The `Patient cannot self-check-in`
acceptance criterion is satisfied entirely by this policy.

## Acceptance Criteria Covered

- AC: Patient cannot self-check-in (staff only) ← `[Authorize(Policy = PolicyNames.Staff)]`
- AC: Staff can search today's appointments by patient name or appointment ID
- AC: Staff marks appointment status from "Scheduled" → "Arrived"
- AC: Arrival timestamp recorded automatically
- AC: SignalR notification sent to provider's dashboard when patient arrives
- AC: If patient arrives > 15 min late, flag as "Late Arrival" (visual indicator)
- Edge: Staff accidentally marks wrong patient → `POST /api/appointments/{id}/revert-arrival`

---

## Implementation Steps

### 1. Add `PatientArrivedPayload` to `NotificationModels.cs`

Edit `src/HealthPlatform.Api/Hubs/NotificationModels.cs`.

Append the new payload record after `QueueStatusUpdatedPayload`:

```csharp
/// <summary>
/// Broadcast to a provider's SignalR group when a patient checks in at
/// the front desk.  The <see cref="IsLateArrival"/> flag drives the
/// "Late Arrival" visual indicator on the provider's dashboard.
/// </summary>
public sealed record PatientArrivedPayload(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    string         PatientFullName,
    DateTimeOffset ArrivalTime,
    bool           IsLateArrival);
```

---

### 2. Add Check-In Actions to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

#### 2a. Add `using` and constructor field for `IHubContext`

Add at the top of the file (with the other usings):

```csharp
using HealthPlatform.Api.Hubs;
using Microsoft.AspNetCore.SignalR;
```

Update the constructor to inject `IHubContext<NotificationHub>`:

```csharp
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISender                       _sender;
    private readonly IHubContext<NotificationHub>  _hub;

    public AppointmentsController(
        ISender                      sender,
        IHubContext<NotificationHub> hub)
    {
        _sender = sender;
        _hub    = hub;
    }
```

#### 2b. Add `SearchToday` action

Add after the existing `RegisterWalkIn` action:

```csharp
/// <summary>
/// Searches today's appointments by patient name fragment or exact appointment ID.
/// Optionally scoped to one provider.  Front-desk staff use this to locate a
/// patient on arrival before marking them as Arrived.
/// </summary>
/// <param name="providerId">Optional provider filter.</param>
/// <param name="patientName">Partial patient name (case-insensitive, min 2 chars).</param>
/// <param name="appointmentId">Exact appointment ID filter.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — list of matching appointments for today.<br/>
/// 422 Unprocessable Entity — no search filter provided, or name too short.
/// </returns>
[HttpGet("today")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(IReadOnlyList<TodayAppointmentItemDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ValidationProblemDetails),               StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> SearchToday(
    [FromQuery] Guid?   providerId,
    [FromQuery] string? patientName,
    [FromQuery] Guid?   appointmentId,
    CancellationToken   ct)
{
    var results = await _sender.Send(
        new TodayAppointmentsSearchQuery(providerId, patientName, appointmentId), ct);
    return Ok(results);
}
```

#### 2c. Add `Arrive` action

```csharp
/// <summary>
/// Marks a booked appointment as Arrived and broadcasts a real-time
/// notification to the provider's dashboard.  Staff and Admin only.
/// </summary>
/// <param name="id">The appointment ID.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — arrival confirmation including late-arrival flag.<br/>
/// 400 Bad Request — appointment status is not Scheduled or Booked.<br/>
/// 404 Not Found — appointment does not exist.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("{id:guid}/arrive")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(ArrivalConfirmationDto),   StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> Arrive(
    [FromRoute] Guid  id,
    CancellationToken ct)
{
    var confirmation = await _sender.Send(new MarkPatientArrivedCommand(id), ct);

    // Broadcast to provider's SignalR group so the dashboard updates in real time
    await _hub.Clients
        .Group($"provider-{confirmation.ProviderId}")
        .SendAsync(
            "PatientArrived",
            new PatientArrivedPayload(
                confirmation.AppointmentId,
                confirmation.ProviderId,
                confirmation.PatientId,
                confirmation.PatientFullName,
                confirmation.ArrivalTime,
                confirmation.IsLateArrival),
            ct);

    return Ok(confirmation);
}
```

#### 2d. Add `RevertArrival` action

```csharp
/// <summary>
/// Reverts an accidental patient check-in back to Scheduled status.
/// Only succeeds within 5 minutes of the original check-in timestamp.
/// Staff and Admin only.
/// </summary>
/// <param name="id">The appointment ID.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — revert confirmation.<br/>
/// 400 Bad Request — appointment is not Arrived, or the 5-minute window has expired.<br/>
/// 404 Not Found — appointment does not exist.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("{id:guid}/revert-arrival")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(RevertArrivalConfirmationDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),               StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails),               StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails),     StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> RevertArrival(
    [FromRoute] Guid  id,
    CancellationToken ct)
{
    var confirmation = await _sender.Send(new RevertArrivalCommand(id), ct);
    return Ok(confirmation);
}
```

#### 2e. Request records

No new request-body records are needed for these actions — `Arrive` and
`RevertArrival` take no request body (the appointment ID comes from the route),
and `SearchToday` uses query-string parameters.

---

## API Surface Summary

| Method | Route | Policy | Responses |
|--------|-------|--------|-----------|
| `GET` | `/api/appointments/today` | Staff | 200, 422 |
| `POST` | `/api/appointments/{id}/arrive` | Staff | 200, 400, 404, 422 |
| `POST` | `/api/appointments/{id}/revert-arrival` | Staff | 200, 400, 404, 422 |

---

## SignalR Client Contract

Providers subscribe to their group via the existing hub method:

```javascript
// Client subscribes once on dashboard load
connection.invoke("SubscribeToProvider", providerId);

// Client handles the arrival event
connection.on("PatientArrived", (payload) => {
  // payload: { appointmentId, providerId, patientId,
  //            patientFullName, arrivalTime, isLateArrival }
  updateQueue(payload);
  if (payload.isLateArrival) showLateArrivalBadge(payload.appointmentId);
});
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

Open Swagger UI at `http://localhost:5013/swagger`:
- `GET  /api/appointments/today`   — visible, requires Staff JWT
- `POST /api/appointments/{id}/arrive`          — visible, returns 200 schema with `isLateArrival`
- `POST /api/appointments/{id}/revert-arrival`  — visible, returns 200

Integration smoke test (requires a JWT with `Staff` or `Admin` role):

```bash
# 1. Search today's appointments for a provider
GET /api/appointments/today?providerId={id}

# 2. Mark patient arrived
POST /api/appointments/{appointmentId}/arrive
# → 200 with { appointmentId, providerId, patientFullName, arrivalTime, isLateArrival }
# → SignalR "PatientArrived" event delivered to provider-{providerId} group

# 3. Revert within 5 min
POST /api/appointments/{appointmentId}/revert-arrival
# → 200 with { appointmentId, status: "Scheduled", message: "..." }

# 4. Revert after 5 min
POST /api/appointments/{appointmentId}/revert-arrival
# → 400 "the 5-minute correction window has expired"
```
