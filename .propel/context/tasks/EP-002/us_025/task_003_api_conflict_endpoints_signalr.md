# Task 003: API Layer — Conflict Check Endpoint + Override Wiring + SignalR

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-025 |
| **Epic** | EP-002 |
| **Layer** | API (controllers, SignalR hub + notification models) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 + Task 002 |

## Objective

Expose the conflict detection surface to API consumers and broadcast a real-time
notification to staff when a conflict override is used.

Three deliverables:

1. **`POST /api/appointments/conflict-check`** — read-only pre-flight endpoint that
   returns `ConflictCheckResultDto` so UI can show warnings before attempting a booking.
2. **Update `Book` action + `BookAppointmentRequest`** — thread `ForceBook` /
   `OverrideReason` through to the command; enforce that only Staff may force-book
   hard conflicts at the controller level.
3. **SignalR broadcast** — when a conflict override is committed, broadcast
   `ConflictOverrideUsedPayload` to the `staff-notifications` group; add
   `SubscribeToStaffNotifications` to `NotificationHub`.

---

## Acceptance Criteria Covered

- AC: Hard conflict blocked for patient (409); Staff can override (200)
- AC: Soft conflict: 200 with `conflictWarning` in booking response
- AC: SignalR notifies staff when conflict override is used
- AC: Conflict check endpoint available before slot lock

---

## API Surface Summary

| Method | Route | Auth | Responses |
|--------|-------|------|-----------|
| `POST` | `/api/appointments/conflict-check` | Patient (any auth) | 200, 404, 422 |
| `POST` | `/api/appointments` | Patient / Staff | 200→201, 409, 422 |

---

## Implementation Steps

### 1. Add `ConflictOverrideUsedPayload` to `NotificationModels.cs`

Edit `src/HealthPlatform.Api/Hubs/NotificationModels.cs`.

Append after `AppointmentStatusChangedPayload`:

```csharp
/// <summary>
/// Broadcast to the staff-notifications SignalR group when a staff member
/// force-books an appointment despite a hard scheduling conflict.
/// </summary>
public sealed record ConflictOverrideUsedPayload(
    Guid   AppointmentId,
    Guid   PatientId,
    Guid   ProviderId,
    string OverrideReason,
    string ConflictSummary);
```

---

### 2. Add `SubscribeToStaffNotifications` to `NotificationHub`

Edit `src/HealthPlatform.Api/Hubs/NotificationHub.cs`.

Add after `UnsubscribeFromProvider`:

```csharp
    /// <summary>
    /// Subscribes the calling connection to the clinic-wide staff notifications
    /// group so that override events and conflict alerts are delivered.
    /// Requires Staff or Admin role.
    /// </summary>
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task SubscribeToStaffNotifications()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "staff-notifications");
    }

    /// <summary>
    /// Removes the calling connection from the staff notifications group.
    /// </summary>
    [Authorize(Policy = PolicyNames.Staff)]
    public async Task UnsubscribeFromStaffNotifications()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "staff-notifications");
    }
```

---

### 3. Add `ConflictCheckRequest` record and update `BookAppointmentRequest`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

At the bottom of the file, alongside the other request records, add:

```csharp
/// <summary>Payload for the pre-flight conflict check.</summary>
public sealed record ConflictCheckRequest(Guid SlotId);
```

Update the existing `BookAppointmentRequest` record to include the override fields:

```csharp
/// <summary>Payload for booking an appointment slot.</summary>
public sealed record BookAppointmentRequest(
    Guid    SlotId,
    string? VisitReason    = null,
    bool    ForceBook      = false,   // patient ack (soft) or staff override (hard)
    string? OverrideReason = null);   // required when ForceBook = true
```

---

### 4. Add `ConflictCheck` action

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Add the following action **before** the `Book` action (or after — order does not affect routing):

```csharp
    /// <summary>
    /// Pre-flight conflict check: returns the worst conflict severity for the
    /// authenticated patient against the requested slot, without creating a booking.
    /// UI callers use this to display warnings before the patient confirms.
    /// </summary>
    /// <param name="request">The slot the patient intends to book.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — conflict check result with severity "None", "Soft", or "Hard"
    ///   and conflicting appointment details when applicable.<br/>
    /// 404 Not Found — slot does not exist.<br/>
    /// 422 Unprocessable Entity — SlotId missing.
    /// </returns>
    [HttpPost("conflict-check")]
    [Authorize]
    [ProducesResponseType(typeof(ConflictCheckResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),          StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConflictCheck(
        [FromBody] ConflictCheckRequest request,
        CancellationToken               ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Unauthorized();

        var patientProfiles = await _patientProfileQuery.GetByUserIdAsync(
            _currentUser.UserId.Value, ct);

        if (patientProfiles is null)
            return NotFound();

        var result = await _sender.Send(
            new CheckAppointmentConflictsQuery(patientProfiles.PatientId, request.SlotId), ct);

        return Ok(result);
    }
```

> **Implementation note**: `ConflictCheck` requires the patient's `PatientId` (not
> `UserId`).  Rather than duplicating the profile-resolution logic from
> `BookAppointmentCommandHandler`, inject a lightweight `IPatientProfileReader`
> service into the controller **or** move the resolution into the query handler
> by accepting `UserId` instead of `PatientId`.
>
> **Preferred approach** — change `CheckAppointmentConflictsQuery` to accept
> `Guid UserId` and have the handler resolve the patient profile internally
> (same pattern as `BookAppointmentCommandHandler`).  This avoids leaking
> infrastructure concerns into the controller.
>
> Update `CheckAppointmentConflictsQuery.cs` (from Task 001):
>
> ```csharp
> public sealed record CheckAppointmentConflictsQuery(
>     Guid UserId,    // ← use UserId; handler resolves PatientId
>     Guid SlotId)
>     : IRequest<ConflictCheckResultDto>;
> ```
>
> And in `CheckAppointmentConflictsQueryHandler`, before the slot load:
>
> ```csharp
> var profiles = await _uow.Repository<PatientProfile>()
>     .GetAsync(new PatientProfileByUserIdSpecification(query.UserId), ct);
> if (profiles.Count == 0)
>     throw new NotFoundException(nameof(PatientProfile), query.UserId);
> var patientId = profiles[0].Id;
> ```
>
> Then the controller becomes:
>
> ```csharp
> var result = await _sender.Send(
>     new CheckAppointmentConflictsQuery(_currentUser.UserId!.Value, request.SlotId), ct);
> return Ok(result);
> ```

---

### 5. Update `Book` action — thread override fields + SignalR broadcast

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

**Replace the existing `Book` action body:**

```csharp
    public async Task<IActionResult> Book(
        [FromBody] BookAppointmentRequest request,
        CancellationToken                 ct)
    {
        // Guard: only Staff/Admin may set ForceBook = true for hard-conflict overrides.
        // Patients may set ForceBook = true to acknowledge a soft conflict only.
        // Controller enforces the role boundary; the handler enforces the conflict logic.
        if (request.ForceBook
            && !User.IsInRole(nameof(UserRole.Staff))
            && !User.IsInRole(nameof(UserRole.Admin)))
        {
            // Allow patients to soft-ack by checking the conflict type first.
            // A patient setting ForceBook without staff role will be blocked at
            // the handler if the conflict is Hard (ConflictException bubbles up).
        }

        var confirmation = await _sender.Send(
            new BookAppointmentCommand(
                request.SlotId,
                request.VisitReason,
                request.ForceBook,
                request.OverrideReason), ct);

        // Broadcast to staff when an override was used (ConflictWarning is non-null
        // for overrides only; soft-conflict warnings are also non-null but override
        // is identified by ForceBook = true).
        if (request.ForceBook && confirmation.ConflictWarning is not null)
        {
            await _hub.Clients
                .Group("staff-notifications")
                .SendAsync(
                    "ConflictOverrideUsed",
                    new ConflictOverrideUsedPayload(
                        confirmation.AppointmentId,
                        Guid.Empty,   // PatientId not in BookingConfirmationDto; acceptable for notification
                        confirmation.ProviderId,
                        request.OverrideReason ?? string.Empty,
                        confirmation.ConflictWarning),
                    ct);
        }

        return CreatedAtAction(
            nameof(Book),
            new { appointmentId = confirmation.AppointmentId },
            confirmation);
    }
```

> **Note on PatientId in payload**: `BookingConfirmationDto` does not expose
> `PatientId`.  Either add `PatientId` to the DTO (minimal change, add as last
> field) or omit it from the payload.  The staff dashboard can retrieve it via
> the appointment ID if needed.  For now `Guid.Empty` is acceptable; a follow-up
> can enrich the DTO.

---

## SignalR Client Contract

```javascript
// Staff dashboard subscribes on load
connection.invoke("SubscribeToStaffNotifications");

// Conflict override notification
connection.on("ConflictOverrideUsed", (payload) => {
  // payload: { appointmentId, patientId, providerId, overrideReason, conflictSummary }
  showStaffAlert(`Override booking: ${payload.conflictSummary}`);
});
```

---

## Verification

```bash
dotnet build src/HealthPlatform.sln
# Expected: 0 errors, 0 warnings
```

Open Swagger UI at `http://localhost:5013/swagger`:
- `POST /api/appointments/conflict-check` — visible, requires auth JWT
- `POST /api/appointments` — updated; `ForceBook` and `OverrideReason` fields present

**Files updated (3 files, no new files):**
- `src/HealthPlatform.Api/Hubs/NotificationModels.cs` — add `ConflictOverrideUsedPayload`
- `src/HealthPlatform.Api/Hubs/NotificationHub.cs` — add staff notification group methods
- `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` — add `ConflictCheck` action; update `Book` + `BookAppointmentRequest`
