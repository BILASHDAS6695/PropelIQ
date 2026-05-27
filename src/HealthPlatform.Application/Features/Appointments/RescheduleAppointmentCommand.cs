using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Reschedules an appointment: cancels the existing booking and creates a
/// new one on the requested slot — atomically within a single SaveChanges.
/// The original visit reason is preserved on the new appointment.
/// If the new slot is unavailable, the current appointment is NOT cancelled.
/// </summary>
public sealed record RescheduleAppointmentCommand(
    Guid               AppointmentId,
    Guid               NewSlotId,
    CancellationReason Reason,
    string?            Note,
    bool               CallerIsStaff) : IRequest<RescheduleConfirmationDto>;

public sealed record RescheduleConfirmationDto(
    Guid           OldAppointmentId,
    Guid           NewAppointmentId,
    DateTimeOffset NewAppointmentTime,
    string         Status);
