# Task 003: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-028 |
| **Epic** | EP-003 |
| **Layer** | API (controller actions + request/response models + authorization) |
| **Priority** | Medium |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 + Task 002 (domain entity, CQRS handlers registered via MediatR) |

## Objective

Expose three REST endpoints on `AppointmentsController` that cover the full
swap-initiation lifecycle: browse available slots, submit a swap request, and
cancel a pending request. All endpoints require patient authentication;
the caller's patient profile ID is read from the JWT claim — never from the
request body — to prevent identity spoofing.

## Acceptance Criteria Covered

- AC: Patient views list of other patients' booked slots (same provider, anonymized)
  → `GET /api/appointments/{id}/swappable-slots`
- AC: Patient selects desired slot and initiates swap request
  → `POST /api/appointments/{id}/swap-requests`
- AC: Requester can cancel pending swap request
  → `DELETE /api/appointments/{id}/swap-requests/{swapRequestId}`

---

## Implementation Steps

### 1. Request/Response Models

Create `src/HealthPlatform.Api/Controllers/AppointmentsController.Swap.cs`
(partial class, same namespace — keeps the controller file manageable):

```csharp
namespace HealthPlatform.Api.Controllers;

/// <summary>Request body for initiating a slot swap.</summary>
public sealed record InitiateSwapRequest(Guid TargetAppointmentId);

/// <summary>Request body for cancelling a swap request.</summary>
public sealed record CancelSwapRequest(string? Reason = null);
```

> **Alternative**: place these inline in the controller file if a separate file
> feels unnecessary for the team.

---

### 2. Add Swap Endpoints to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Add the following three action methods. The controller already injects `ISender`
and `IHttpContextAccessor` (or reads `User` from `ControllerBase`) — use the
existing pattern to extract the patient ID from claims.

#### 2a. Helper — extract caller's patient ID from JWT

Add a private helper at the bottom of the class:

```csharp
private Guid GetCallerPatientId()
{
    var raw = User.FindFirstValue("patientProfileId")
           ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
           ?? throw new UnauthorizedAccessException("Patient profile claim missing.");
    return Guid.Parse(raw);
}
```

> Check the existing `AuthController` / `PatientOwnershipHandler` to confirm
> the exact claim name used for the patient profile ID in this codebase.

#### 2b. `GET /api/appointments/{id}/swappable-slots`

```csharp
/// <summary>
/// Returns all booked slots for the same provider that are eligible for swap
/// with the specified appointment. Patient identity is anonymized — only
/// slot times are returned.
/// </summary>
/// <param name="id">The requester's appointment ID.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — list of <c>SwappableSlotDto</c>.<br/>
/// 403 Forbidden — caller does not own the appointment.<br/>
/// 404 Not Found — appointment does not exist.
/// </returns>
[HttpGet("{id:guid}/swappable-slots")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(typeof(IReadOnlyList<SwappableSlotDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetSwappableSlots(
    [FromRoute] Guid id,
    CancellationToken ct)
{
    var slots = await _sender.Send(new GetSwappableSlotsQuery(id), ct);
    return Ok(slots);
}
```

#### 2c. `POST /api/appointments/{id}/swap-requests`

```csharp
/// <summary>
/// Initiates a slot swap request. The caller offers their current appointment
/// slot in exchange for the target appointment's slot.
/// </summary>
/// <param name="id">The requester's appointment ID (offered slot).</param>
/// <param name="request">Contains the target appointment ID.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 201 Created — <c>SwapRequestDto</c> with swap request details.<br/>
/// 403 Forbidden — caller does not own the appointment.<br/>
/// 404 Not Found — requester or target appointment not found.<br/>
/// 409 Conflict — active swap request already exists, or target not eligible.
/// </returns>
[HttpPost("{id:guid}/swap-requests")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status201Created)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> InitiateSwapRequest(
    [FromRoute] Guid           id,
    [FromBody]  InitiateSwapRequest request,
    CancellationToken              ct)
{
    var patientId = GetCallerPatientId();

    var result = await _sender.Send(
        new InitiateSwapRequestCommand(patientId, id, request.TargetAppointmentId), ct);

    return CreatedAtAction(
        nameof(GetSwappableSlots),
        new { id },
        result);
}
```

#### 2d. `DELETE /api/appointments/{id}/swap-requests/{swapRequestId}`

```csharp
/// <summary>
/// Cancels a pending swap request initiated by the calling patient.
/// </summary>
/// <param name="id">The requester's appointment ID.</param>
/// <param name="swapRequestId">The swap request to cancel.</param>
/// <param name="request">Optional cancellation reason.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 204 No Content — swap request cancelled successfully.<br/>
/// 403 Forbidden — caller did not initiate this swap request.<br/>
/// 404 Not Found — swap request does not exist.<br/>
/// 409 Conflict — swap request is not in Pending status.
/// </returns>
[HttpDelete("{id:guid}/swap-requests/{swapRequestId:guid}")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> CancelSwapRequest(
    [FromRoute] Guid             id,
    [FromRoute] Guid             swapRequestId,
    [FromBody]  CancelSwapRequest request,
    CancellationToken             ct)
{
    var patientId = GetCallerPatientId();

    await _sender.Send(
        new CancelSwapRequestCommand(patientId, swapRequestId, request.Reason), ct);

    return NoContent();
}
```

---

### 3. Verify `GlobalExceptionHandler` Maps New Exceptions

Open `src/HealthPlatform.Api/Middleware/GlobalExceptionHandler.cs` and confirm
(or add) mappings for:

| Exception | HTTP Status |
|-----------|-------------|
| `NotFoundException` | 404 Not Found |
| `ForbiddenException` | 403 Forbidden |
| `ConflictException` | 409 Conflict |

These should already exist from prior stories (US-020/US-021). No change needed
if they are present.

---

### 4. Required `using` Directives

Add at the top of `AppointmentsController.cs` (only the missing ones):

```csharp
using HealthPlatform.Application.Features.SlotSwap;
using System.Security.Claims;
```

---

### 5. Smoke-Test the Endpoints

With the API running (`dotnet run --launch-profile http`), test the endpoints
using curl, Postman, or the Swagger UI at `http://localhost:5013/swagger`:

```bash
# 1. Login as a patient to get a JWT
POST /api/auth/login  { "email": "...", "password": "..." }

# 2. Browse swappable slots for your appointment
GET /api/appointments/{your-appointment-id}/swappable-slots
Authorization: Bearer <token>

# 3. Initiate a swap request
POST /api/appointments/{your-appointment-id}/swap-requests
Authorization: Bearer <token>
{ "targetAppointmentId": "<target-id>" }

# 4. Cancel the swap
DELETE /api/appointments/{your-appointment-id}/swap-requests/{swap-request-id}
Authorization: Bearer <token>
{ "reason": "Changed my mind" }
```

Expected responses:
- Step 2: `200 OK` — `[ { "appointmentId": "...", "slotTime": "..." } ]`
- Step 3: `201 Created` — `{ "swapRequestId": "...", "status": "Pending", ... }`
- Step 4: `204 No Content`

---

## Definition of Done

- [ ] `InitiateSwapRequest` and `CancelSwapRequest` request models defined
- [ ] `GetSwappableSlots` action added to `AppointmentsController`
- [ ] `InitiateSwapRequest` action added (POST, returns 201)
- [ ] `CancelSwapRequest` action added (DELETE, returns 204)
- [ ] `GetCallerPatientId()` helper reads patient ID from JWT claim
- [ ] All three endpoints guarded with `[Authorize(Policy = PolicyNames.Patient)]`
- [ ] `GlobalExceptionHandler` maps `NotFoundException`, `ForbiddenException`, `ConflictException` correctly
- [ ] `dotnet build` succeeds with no errors
- [ ] Smoke tests pass against `http://localhost:5013`
