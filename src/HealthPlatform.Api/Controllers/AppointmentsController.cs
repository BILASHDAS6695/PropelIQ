using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.SlotSwap;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient appointment booking endpoints.
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISender _sender;

    public AppointmentsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Books an available appointment slot for the authenticated patient.
    /// </summary>
    /// <param name="request">Booking payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — booking confirmation with provider name, date, time,
    ///   and appointment ID.<br/>
    /// 409 Conflict — slot no longer available, or duplicate active appointment
    ///   on the same provider/day (ConflictException → GlobalExceptionHandler).<br/>
    /// 401 Unauthorized — user not authenticated.<br/>
    /// 404 Not Found — slot or provider does not exist.<br/>
    /// 422 Unprocessable Entity — validation failed (ValidationBehavior pipeline).
    /// </returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingConfirmationDto),   StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Book(
        [FromBody] BookAppointmentRequest request,
        CancellationToken                 ct)
    {
        var confirmation = await _sender.Send(
            new BookAppointmentCommand(request.SlotId, request.VisitReason), ct);

        return CreatedAtAction(
            nameof(Book),
            new { appointmentId = confirmation.AppointmentId },
            confirmation);
    }

    /// <summary>
    /// Registers a walk-in appointment for an existing patient.
    /// Does not consume a pre-booked slot; auto-assigns a queue position.
    /// </summary>
    /// <param name="request">Walk-in registration payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — walk-in confirmation with queue position and arrival time.<br/>
    /// 404 Not Found — patient or provider does not exist.<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost("walk-in")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(WalkInConfirmationDto),    StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RegisterWalkIn(
        [FromBody] RegisterWalkInRequest request,
        CancellationToken                ct)
    {
        var confirmation = await _sender.Send(
            new RegisterWalkInCommand(request.PatientId, request.ProviderId, request.VisitReason), ct);

        return CreatedAtAction(
            nameof(RegisterWalkIn),
            new { appointmentId = confirmation.AppointmentId },
            confirmation);
    }

    /// <summary>
    /// Returns all booked slots for the same provider that are eligible for swap
    /// with the specified appointment. Patient identity is anonymized — only
    /// slot times are returned.
    /// </summary>
    /// <param name="id">The requester's appointment ID.</param>
    /// <param name="ct">Cancellation token.</param>
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

    /// <summary>
    /// Initiates a slot swap request. The caller offers their current appointment
    /// slot in exchange for the target appointment's slot.
    /// </summary>
    /// <param name="id">The requester's appointment ID (offered slot).</param>
    /// <param name="request">Contains the target appointment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{id:guid}/swap-requests")]
    [Authorize(Policy = PolicyNames.Patient)]
    [ProducesResponseType(typeof(SwapRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateSwapRequest(
        [FromRoute] Guid               id,
        [FromBody]  InitiateSwapRequest request,
        CancellationToken              ct)
    {
        var result = await _sender.Send(
            new InitiateSwapRequestCommand(id, request.TargetAppointmentId), ct);

        return CreatedAtAction(nameof(GetSwappableSlots), new { id }, result);
    }

    /// <summary>
    /// Cancels a pending swap request initiated by the calling patient.
    /// </summary>
    /// <param name="id">The requester's appointment ID.</param>
    /// <param name="swapRequestId">The swap request to cancel.</param>
    /// <param name="request">Optional cancellation reason.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{id:guid}/swap-requests/{swapRequestId:guid}")]
    [Authorize(Policy = PolicyNames.Patient)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSwapRequest(
        [FromRoute] Guid              id,
        [FromRoute] Guid              swapRequestId,
        [FromBody]  CancelSwapRequest? request,
        CancellationToken             ct)
    {
        await _sender.Send(
            new CancelSwapRequestCommand(swapRequestId, request?.Reason), ct);

        return NoContent();
    }

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
}

/// <summary>Payload for booking an appointment slot.</summary>
public sealed record BookAppointmentRequest(
    Guid    SlotId,
    string? VisitReason = null);

/// <summary>Payload for registering a walk-in appointment.</summary>
public sealed record RegisterWalkInRequest(
    Guid    PatientId,
    Guid    ProviderId,
    string? VisitReason = null);

/// <summary>Request body for initiating a slot swap.</summary>
public sealed record InitiateSwapRequest(Guid TargetAppointmentId);

/// <summary>Request body for cancelling a swap request.</summary>
public sealed record CancelSwapRequest(string? Reason = null);

/// <summary>Request body for responding to a slot swap offer.</summary>
public sealed record RespondToSwapRequest(bool Accept, string? Reason = null);
