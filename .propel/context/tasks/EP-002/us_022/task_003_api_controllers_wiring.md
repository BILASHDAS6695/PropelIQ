# Task 003: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-022 |
| **Epic** | EP-002 |
| **Layer** | API (controllers + request models) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 + Task 002 (domain + CQRS handlers registered) |

## Objective

Expose cancellation and reschedule as two new POST actions on the existing
`AppointmentsController`.  The controller resolves whether the caller is Staff
from the JWT role claim and injects that flag into the command so the handler
can apply (or bypass) the 2-hour time restriction without reaching back into
the HTTP context.

## Acceptance Criteria Covered

- AC: Patient can cancel their own appointment (`POST /api/appointments/{id}/cancel`)
- AC: Staff can cancel any appointment with no time restriction (same endpoint, role-elevated)
- AC: Patient/staff can reschedule (`POST /api/appointments/{id}/reschedule`)
- AC: Reschedule shows available slots (slot lookup reuses the existing `AppointmentSlot` query path; the UI calls `GET /api/providers/{id}/slots` from US-020)

---

## Implementation Steps

### 1. Add Cancel and Reschedule Actions to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Add the two new request records at the bottom of the file (alongside the existing
`BookAppointmentRequest` and `RegisterWalkInRequest` records):

```csharp
/// <summary>Payload for cancelling an appointment.</summary>
public sealed record CancelAppointmentRequest(
    string  Reason,
    string? Note = null);

/// <summary>Payload for rescheduling an appointment.</summary>
public sealed record RescheduleAppointmentRequest(
    Guid    NewSlotId,
    string  Reason,
    string? Note = null);
```

Add the two new actions inside `AppointmentsController`, after the existing
`RegisterWalkIn` action:

```csharp
/// <summary>
/// Cancels an existing appointment.
/// Patients may only cancel their own appointment and only when more than
/// 2 hours remain until the start time.  Staff and Admin can cancel any
/// appointment regardless of the time remaining.
/// </summary>
/// <param name="id">The appointment ID.</param>
/// <param name="request">Cancellation reason and optional note.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — cancellation confirmation.<br/>
/// 400 Bad Request — appointment already Arrived/Completed, or < 2 h window (patient).<br/>
/// 403 Forbidden — patient trying to cancel another patient's appointment.<br/>
/// 404 Not Found — appointment does not exist.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("{id:guid}/cancel")]
[Authorize]
[ProducesResponseType(typeof(CancellationConfirmationDto),  StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails),      StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> Cancel(
    [FromRoute] Guid                   id,
    [FromBody]  CancelAppointmentRequest request,
    CancellationToken                  ct)
{
    if (!Enum.TryParse<CancellationReason>(request.Reason, ignoreCase: true, out var reason))
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title  = "Bad Request",
            Detail = $"'{request.Reason}' is not a valid cancellation reason. " +
                     "Allowed values: ScheduleConflict, FeelingBetter, Other."
        });

    var confirmation = await _sender.Send(
        new CancelAppointmentCommand(
            id,
            reason,
            request.Note,
            CallerIsStaff: User.IsInRole(nameof(UserRole.Staff))
                        || User.IsInRole(nameof(UserRole.Admin))), ct);

    return Ok(confirmation);
}

/// <summary>
/// Reschedules an existing appointment: cancels the current booking and
/// creates a new one on the requested slot in a single atomic operation.
/// The original visit reason is preserved.  If the new slot is unavailable
/// the current appointment is not cancelled (409 Conflict returned instead).
/// </summary>
/// <param name="id">The appointment ID to reschedule.</param>
/// <param name="request">New slot ID, cancellation reason, and optional note.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 201 Created — reschedule confirmation with new appointment ID and time.<br/>
/// 400 Bad Request — appointment already Arrived/Completed, or < 2 h window (patient).<br/>
/// 403 Forbidden — patient trying to reschedule another patient's appointment.<br/>
/// 404 Not Found — appointment or new slot does not exist.<br/>
/// 409 Conflict — new slot is no longer available; existing appointment unchanged.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("{id:guid}/reschedule")]
[Authorize]
[ProducesResponseType(typeof(RescheduleConfirmationDto),    StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails),                StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ValidationProblemDetails),      StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> Reschedule(
    [FromRoute] Guid                       id,
    [FromBody]  RescheduleAppointmentRequest request,
    CancellationToken                      ct)
{
    if (!Enum.TryParse<CancellationReason>(request.Reason, ignoreCase: true, out var reason))
        return BadRequest(new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title  = "Bad Request",
            Detail = $"'{request.Reason}' is not a valid cancellation reason. " +
                     "Allowed values: ScheduleConflict, FeelingBetter, Other."
        });

    var confirmation = await _sender.Send(
        new RescheduleAppointmentCommand(
            id,
            request.NewSlotId,
            reason,
            request.Note,
            CallerIsStaff: User.IsInRole(nameof(UserRole.Staff))
                        || User.IsInRole(nameof(UserRole.Admin))), ct);

    return CreatedAtAction(
        nameof(Reschedule),
        new { appointmentId = confirmation.NewAppointmentId },
        confirmation);
}
```

### 2. Add Missing `using` Directives

Ensure the following `using` statements are present at the top of
`AppointmentsController.cs`:

```csharp
using HealthPlatform.Domain.Enums;
```

---

## API Surface Summary

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `POST` | `/api/appointments/{id}/cancel` | Any authenticated | Cancel appointment; staff bypass 2 h rule |
| `POST` | `/api/appointments/{id}/reschedule` | Any authenticated | Reschedule atomically; new slot checked first |

### Example Request — Cancel

```http
POST /api/appointments/3fa85f64-5717-4562-b3fc-2c963f66afa6/cancel
Authorization: Bearer <token>
Content-Type: application/json

{
  "reason": "ScheduleConflict"
}
```

### Example Response — Cancel (200 OK)

```json
{
  "appointmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "status": "Cancelled",
  "cancellationReason": "ScheduleConflict"
}
```

### Example Request — Reschedule

```http
POST /api/appointments/3fa85f64-5717-4562-b3fc-2c963f66afa6/reschedule
Authorization: Bearer <token>
Content-Type: application/json

{
  "newSlotId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "reason": "ScheduleConflict"
}
```

### Example Response — Reschedule (201 Created)

```json
{
  "oldAppointmentId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "newAppointmentId": "9b2a8b36-1e47-4d5b-9c1d-3a7f84b92c10",
  "newAppointmentTime": "2026-06-01T10:00:00+00:00",
  "status": "Scheduled"
}
```

---

## Verification Checklist

- [ ] `POST /api/appointments/{id}/cancel` returns 200 for a valid patient cancellation > 2 h before start
- [ ] `POST /api/appointments/{id}/cancel` returns 400 when the appointment is in Arrived status
- [ ] `POST /api/appointments/{id}/cancel` returns 400 when patient cancels < 2 h before start
- [ ] `POST /api/appointments/{id}/cancel` with a Staff JWT succeeds regardless of time remaining
- [ ] `POST /api/appointments/{id}/cancel` returns 403 when patient cancels another patient's appointment
- [ ] `POST /api/appointments/{id}/reschedule` returns 409 and leaves the original appointment untouched when the new slot is unavailable
- [ ] `POST /api/appointments/{id}/reschedule` returns 201 and old appointment shows Cancelled in DB
- [ ] Freed slot is immediately queryable as Available by other patients
- [ ] `dotnet build src/HealthPlatform.sln` compiles without errors
- [ ] Swagger (`/swagger`) shows both new endpoints with correct response codes
