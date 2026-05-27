# Task 003: API Controller — Respond Endpoint

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-029 |
| **Epic** | EP-003 |
| **Layer** | API (controller action + request model + authorization) |
| **Priority** | Medium |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | Task 001 of this story (`RespondToSwapRequestCommand`, `SwapResponseDto` registered via MediatR) |

## Objective

Expose a single `POST` endpoint that the target patient calls to accept or decline
a pending swap request. The caller's identity is always read from the JWT — never
from the request body — to prevent spoofing.

## Acceptance Criteria Covered

- AC: Target patient can Accept or Decline from notification or in-app
  → `POST /api/appointments/{id}/swap-requests/{swapRequestId}/respond`

---

## Implementation Steps

### 1. Add Request Model

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`.

Append the following record at the bottom of the file alongside the existing
`InitiateSwapRequest` and `CancelSwapRequest` records:

```csharp
/// <summary>Request body for responding to a slot swap offer.</summary>
public sealed record RespondToSwapRequest(bool Accept, string? Reason = null);
```

---

### 2. Add Endpoint to `AppointmentsController`

Insert the following action method inside `AppointmentsController`, after the existing
`CancelSwapRequest` action and before the closing brace `}` of the class:

```csharp
/// <summary>
/// Accepts or declines a pending slot swap request. Must be called by the
/// patient who owns the target appointment (the offer recipient).
/// On accept, both appointments' slot times are swapped atomically and both
/// parties receive an email confirmation.
/// On decline, the requester is notified and the swap request is closed.
/// </summary>
/// <param name="id">The target appointment ID (the caller's appointment).</param>
/// <param name="swapRequestId">The swap request to respond to.</param>
/// <param name="request">Accept flag and optional decline reason.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — <c>SwapResponseDto</c> with updated status and new slot times (if accepted).<br/>
/// 403 Forbidden — caller is not the target patient of this swap request.<br/>
/// 404 Not Found — swap request does not exist.<br/>
/// 409 Conflict — swap request is not Pending, has expired, or either appointment
///   is no longer eligible for swap.
/// </returns>
[HttpPost("{id:guid}/swap-requests/{swapRequestId:guid}/respond")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(typeof(SwapResponseDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<IActionResult> RespondToSwapRequest(
    [FromRoute] Guid                 id,
    [FromRoute] Guid                 swapRequestId,
    [FromBody]  RespondToSwapRequest request,
    CancellationToken                ct)
{
    var result = await _sender.Send(
        new RespondToSwapRequestCommand(swapRequestId, request.Accept, request.Reason), ct);

    return Ok(result);
}
```

> **Note**: `id` (the target appointment ID) is available in the route for RESTful
> consistency and can be used for future endpoint-level authorization middleware.
> The handler resolves the actual patient identity from the JWT — `id` is not
> passed into the command to prevent client-supplied ID spoofing.

---

## REST Contract Summary

| Method | Route | Auth | Success | Error codes |
|--------|-------|------|---------|-------------|
| `POST` | `/api/appointments/{id}/swap-requests/{swapRequestId}/respond` | Patient JWT | `200 SwapResponseDto` | 403, 404, 409 |

### Request Body

```json
{
  "accept": true,
  "reason": null
}
```

### Response Body (accept = true)

```json
{
  "swapRequestId": "3fa85f64-...",
  "status": "Accepted",
  "requesterNewSlotTime": "2026-06-10T09:00:00+00:00",
  "targetNewSlotTime": "2026-06-10T10:30:00+00:00"
}
```

### Response Body (accept = false)

```json
{
  "swapRequestId": "3fa85f64-...",
  "status": "Declined",
  "requesterNewSlotTime": null,
  "targetNewSlotTime": null
}
```

---

## Files Modified

| Action | Path |
|--------|------|
| EDIT   | `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` |

## Verification

- `dotnet build src/HealthPlatform.Api` → 0 errors
- `POST /api/appointments/{id}/swap-requests/{swapId}/respond` with a valid
  Patient JWT → 200 OK with `SwapResponseDto`
- Same endpoint called with a non-Patient JWT → 403 Forbidden (policy enforcement)
- Route with a non-existent `swapRequestId` → 404 Not Found (via `GlobalExceptionHandler`)
- Calling with `accept: true` when the swap is already `Accepted` → 409 Conflict
