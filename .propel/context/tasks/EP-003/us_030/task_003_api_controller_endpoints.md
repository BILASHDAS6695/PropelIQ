# Task 003: API Layer — Staff Swap Mediation Endpoints

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-030 |
| **Epic** | EP-003 |
| **Layer** | API (controller, request models, authorization) |
| **Priority** | Low |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 002 of this story (`GetPendingSwapRequestsQuery`, `StaffMediateSwapCommand`, `StaffReassignSlotsCommand`, `StaffMediationResultDto`, `PendingSwapRequestSummaryDto` registered via MediatR) |

## Objective

Expose three staff-only endpoints under `api/staff/swap-requests` that allow staff
members to view all pending swap requests (with both patient names) and to mediate
them via force-approve, force-decline, or three-way slot reassignment.

All endpoints are protected by the `Staff` authorization policy
(`PolicyNames.Staff` → Staff and Admin roles only). Patient role is excluded.

## Acceptance Criteria Covered

- AC: Staff dashboard shows all pending swap requests (both patient names visible to staff)
- AC: Staff can force-approve, force-decline, or initiate three-way swap via dedicated endpoints
- AC: All staff override actions require a reason text (enforced by the command validator — 422 if missing)

---

## Implementation Steps

### 1. Create `StaffSwapController`

Create `src/HealthPlatform.Api/Controllers/StaffSwapController.cs`:

```csharp
using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.SlotSwap;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Staff-only endpoints for viewing and mediating pending slot swap requests.
/// </summary>
[ApiController]
[Route("api/staff/swap-requests")]
[Authorize(Policy = PolicyNames.Staff)]
public sealed class StaffSwapController : ControllerBase
{
    private readonly ISender _sender;

    public StaffSwapController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns all pending slot swap requests, including both patient names.
    /// Ordered by <c>ExpiresAt</c> ascending so the most urgent requests appear first.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — list of <c>PendingSwapRequestSummaryDto</c> (may be empty).
    /// </returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingSwapRequestSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingSwapRequests(CancellationToken ct)
    {
        var result = await _sender.Send(new GetPendingSwapRequestsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Force-approves or force-declines a pending swap request on behalf of the target patient.
    /// <para>
    /// Force-approve atomically swaps both appointments' slot times, notifies both patients,
    /// and logs the action to the audit trail with the staff member's user ID and reason.
    /// </para>
    /// <para>
    /// Force-decline closes the swap request without changing any appointments,
    /// notifies both patients, and logs the action.
    /// </para>
    /// </summary>
    /// <param name="swapRequestId">ID of the pending swap request to mediate.</param>
    /// <param name="request">Override payload: approve flag and mandatory reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — <c>StaffMediationResultDto</c> with outcome status, staff ID, and new slot times.<br/>
    /// 404 Not Found — swap request does not exist.<br/>
    /// 409 Conflict — swap request is not Pending / has expired / a deactivated patient is involved
    ///   / concurrent mediation detected.<br/>
    /// 422 Unprocessable Entity — <c>Reason</c> is missing or exceeds 500 characters.
    /// </returns>
    [HttpPost("{swapRequestId:guid}/mediate")]
    [ProducesResponseType(typeof(StaffMediationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> MediateSwapRequest(
        [FromRoute] Guid                   swapRequestId,
        [FromBody]  StaffMediateSwapRequest request,
        CancellationToken                   ct)
    {
        var result = await _sender.Send(
            new StaffMediateSwapCommand(swapRequestId, request.ForceApprove, request.Reason), ct);

        return Ok(result);
    }

    /// <summary>
    /// Performs a three-way slot reassignment: the requester acquires the target's current
    /// slot, and the target is moved to a staff-supplied available slot at the same provider.
    /// All slot and appointment changes are committed in a single transaction.
    /// </summary>
    /// <param name="request">Reassignment payload: swap request ID, new target slot ID, and mandatory reason.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — <c>StaffMediationResultDto</c> with outcome status and both new slot times.<br/>
    /// 404 Not Found — swap request or new target slot does not exist.<br/>
    /// 409 Conflict — swap request is not Pending / slot is not available / different provider /
    ///   deactivated patient / concurrent mediation detected.<br/>
    /// 422 Unprocessable Entity — any required field is missing or invalid.
    /// </returns>
    [HttpPost("reassign")]
    [ProducesResponseType(typeof(StaffMediationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReassignSlots(
        [FromBody] StaffReassignRequest request,
        CancellationToken               ct)
    {
        var result = await _sender.Send(
            new StaffReassignSlotsCommand(
                request.SwapRequestId,
                request.NewTargetSlotId,
                request.Reason), ct);

        return Ok(result);
    }
}

// ── Request models ────────────────────────────────────────────────────────────

/// <summary>Request body for staff force-approve or force-decline.</summary>
public sealed record StaffMediateSwapRequest(bool ForceApprove, string Reason);

/// <summary>Request body for staff three-way slot reassignment.</summary>
public sealed record StaffReassignRequest(Guid SwapRequestId, Guid NewTargetSlotId, string Reason);
```

---

## REST Contract Summary

### GET `/api/staff/swap-requests/pending`

| Field | Value |
|-------|-------|
| **Auth** | Staff or Admin JWT (`PolicyNames.Staff`) |
| **Success** | `200 OK` — `PendingSwapRequestSummaryDto[]` |

**Response Body Example:**

```json
[
  {
    "swapRequestId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "requesterPatientId": "a1b2c3d4-...",
    "requesterFullName": "Alice Smith",
    "requesterSlotTime": "2026-06-10T09:00:00+00:00",
    "targetPatientId": "e5f6a7b8-...",
    "targetFullName": "Bob Jones",
    "targetSlotTime": "2026-06-10T10:30:00+00:00",
    "expiresAt": "2026-06-11T09:00:00+00:00"
  }
]
```

---

### POST `/api/staff/swap-requests/{swapRequestId}/mediate`

| Field | Value |
|-------|-------|
| **Auth** | Staff or Admin JWT (`PolicyNames.Staff`) |
| **Success** | `200 OK` — `StaffMediationResultDto` |
| **Errors** | 404, 409, 422 |

**Request Body (force-approve):**

```json
{
  "forceApprove": true,
  "reason": "Patient Alice has travel obligations and cannot rearrange independently."
}
```

**Request Body (force-decline):**

```json
{
  "forceApprove": false,
  "reason": "Target patient has a medical condition requiring this specific time slot."
}
```

**Response Body:**

```json
{
  "swapRequestId": "3fa85f64-...",
  "status": "StaffApproved",
  "mediatedByUserId": "d9e0f1a2-...",
  "overriddenAt": "2026-06-10T08:45:00+00:00",
  "requesterNewSlotTime": "2026-06-10T10:30:00+00:00",
  "targetNewSlotTime": "2026-06-10T09:00:00+00:00"
}
```

---

### POST `/api/staff/swap-requests/reassign`

| Field | Value |
|-------|-------|
| **Auth** | Staff or Admin JWT (`PolicyNames.Staff`) |
| **Success** | `200 OK` — `StaffMediationResultDto` |
| **Errors** | 404, 409, 422 |

**Request Body:**

```json
{
  "swapRequestId": "3fa85f64-...",
  "newTargetSlotId": "c3d4e5f6-...",
  "reason": "Scheduling conflict resolved by assigning an open morning slot to Patient Bob."
}
```

**Response Body:**

```json
{
  "swapRequestId": "3fa85f64-...",
  "status": "StaffReassigned",
  "mediatedByUserId": "d9e0f1a2-...",
  "overriddenAt": "2026-06-10T08:47:00+00:00",
  "requesterNewSlotTime": "2026-06-10T10:30:00+00:00",
  "targetNewSlotTime": "2026-06-10T11:00:00+00:00"
}
```

---

## Files Created

| Action | Path |
|--------|------|
| CREATE | `src/HealthPlatform.Api/Controllers/StaffSwapController.cs` |

## Verification

- `dotnet build src/HealthPlatform.Api` → 0 errors
- `GET /api/staff/swap-requests/pending` with **Patient** JWT → 403 Forbidden (Staff policy enforced)
- `GET /api/staff/swap-requests/pending` with **Staff** JWT → 200 OK with list
- `POST /api/staff/swap-requests/{id}/mediate` with `forceApprove: true` and valid `reason` → 200 OK, status `StaffApproved`
- `POST /api/staff/swap-requests/{id}/mediate` with missing `reason` → 422 Unprocessable Entity
- `POST /api/staff/swap-requests/reassign` with an unavailable `newTargetSlotId` → 409 Conflict
- `POST /api/staff/swap-requests/{id}/mediate` on an already-mediated swap → 409 Conflict
