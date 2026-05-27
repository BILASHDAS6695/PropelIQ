using HealthPlatform.Api.Authorization;
using HealthPlatform.Api.Hubs;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.SlotSwap;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient appointment booking endpoints.
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISender                      _sender;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ICurrentUserService          _currentUser;

    public AppointmentsController(
        ISender                      sender,
        IHubContext<NotificationHub> hub,
        ICurrentUserService          currentUser)
    {
        _sender      = sender;
        _hub         = hub;
        _currentUser = currentUser;
    }

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
            new BookAppointmentCommand(
                request.SlotId,
                request.VisitReason,
                request.ForceBook,
                request.OverrideReason), ct);

        // Broadcast to staff when a conflict override was committed.
        if (request.ForceBook && confirmation.ConflictWarning is not null)
        {
            await _hub.Clients
                .Group("staff-notifications")
                .SendAsync(
                    "ConflictOverrideUsed",
                    new ConflictOverrideUsedPayload(
                        confirmation.AppointmentId,
                        Guid.Empty,   // PatientId not in BookingConfirmationDto; staff can look up by AppointmentId
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
    [ProducesResponseType(typeof(ConflictCheckResultDto),   StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ConflictCheck(
        [FromBody] ConflictCheckRequest request,
        CancellationToken               ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Unauthorized();

        var result = await _sender.Send(
            new CheckAppointmentConflictsQuery(_currentUser.UserId.Value, request.SlotId), ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns all appointments for the currently authenticated patient, ordered by
    /// slot time descending (most recent first).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — list of patient appointments (may be empty).<br/>
    /// 401 Unauthorized — user not authenticated.<br/>
    /// 404 Not Found — patient profile not found.
    /// </returns>
    [HttpGet("mine")]
    [Authorize(Policy = PolicyNames.Patient)]
    [ProducesResponseType(typeof(IReadOnlyList<PatientAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var results = await _sender.Send(new GetMyAppointmentsQuery(), ct);
        return Ok(results);
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
    [ProducesResponseType(typeof(ArrivalConfirmationDto),  StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Arrive(
        [FromRoute] Guid  id,
        CancellationToken ct)
    {
        var confirmation = await _sender.Send(new MarkPatientArrivedCommand(id), ct);

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

    /// <summary>
    /// Marks an appointment as NoShow (manual staff action).
    /// The associated slot is freed immediately and a follow-up email is sent
    /// to the patient.
    /// </summary>
    /// <param name="id">Appointment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — NoShow confirmed with updated patient no-show count.<br/>
    /// 400 Bad Request — appointment is not in Scheduled or Booked state.<br/>
    /// 401 Unauthorized — caller is not authenticated.<br/>
    /// 403 Forbidden — caller does not have Staff or Admin role.<br/>
    /// 404 Not Found — appointment does not exist.
    /// </returns>
    [HttpPost("{id:guid}/no-show")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(NoShowConfirmationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),         StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),         StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkNoShow(
        [FromRoute] Guid  id,
        CancellationToken ct)
    {
        var result = await _sender.Send(new MarkNoShowCommand(id, IsAutomatic: false), ct);

        await _hub.Clients
            .Group($"provider-{result.ProviderId}")
            .SendAsync("AppointmentNoShow", new AppointmentNoShowPayload(
                AppointmentId:           result.AppointmentId,
                ProviderId:              result.ProviderId,
                PatientId:               result.PatientId,
                SlotTime:                result.SlotTime,
                IsAutomatic:             false,
                PatientTotalNoShowCount: result.PatientTotalNoShowCount), ct);

        return Ok(result);
    }

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
    string? VisitReason    = null,
    bool    ForceBook      = false,   // patient ack (soft) or staff override (hard)
    string? OverrideReason = null);   // required when ForceBook = true

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

/// <summary>Payload for cancelling an appointment.</summary>
public sealed record CancelAppointmentRequest(
    string  Reason,
    string? Note = null);

/// <summary>Payload for rescheduling an appointment.</summary>
public sealed record RescheduleAppointmentRequest(
    Guid    NewSlotId,
    string  Reason,
    string? Note = null);

/// <summary>Payload for updating an appointment status.</summary>
public sealed record UpdateStatusRequest(string NewStatus);

/// <summary>Payload for the pre-flight conflict check.</summary>
public sealed record ConflictCheckRequest(Guid SlotId);
