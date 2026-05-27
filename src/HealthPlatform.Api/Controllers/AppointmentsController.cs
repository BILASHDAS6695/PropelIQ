using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Appointments;
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
