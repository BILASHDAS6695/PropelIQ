using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.SlotSwap;
using HealthPlatform.Domain.Enums;
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
    /// 400 Bad Request — appointment already Arrived/Completed, or &lt; 2 h window (patient).<br/>
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
        [FromRoute] Guid                    id,
        [FromBody]  CancelAppointmentRequest request,
        CancellationToken                   ct)
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
    /// 400 Bad Request — appointment already Arrived/Completed, or &lt; 2 h window (patient).<br/>
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
        [FromRoute] Guid                        id,
        [FromBody]  RescheduleAppointmentRequest request,
        CancellationToken                       ct)
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

/// <summary>Payload for cancelling an appointment.</summary>
public sealed record CancelAppointmentRequest(
    string  Reason,
    string? Note = null);

/// <summary>Payload for rescheduling an appointment.</summary>
public sealed record RescheduleAppointmentRequest(
    Guid    NewSlotId,
    string  Reason,
    string? Note = null);
