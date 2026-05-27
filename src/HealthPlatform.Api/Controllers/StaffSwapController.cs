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
