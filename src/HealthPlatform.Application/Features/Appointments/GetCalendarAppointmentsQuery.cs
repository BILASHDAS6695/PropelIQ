using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns appointments within a date range for the calendar view.
/// - Patient callers: returns own appointments (ProviderId filter ignored).
/// - Staff/Admin callers: returns all appointments for the given provider
///   (or all providers when ProviderId is null).
/// </summary>
public sealed record GetCalendarAppointmentsQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    Guid?          ProviderId = null) : IRequest<IReadOnlyList<CalendarAppointmentDto>>;

/// <summary>Appointment summary returned for calendar rendering.</summary>
public sealed record CalendarAppointmentDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    string         PatientName,
    DateTimeOffset SlotTime,
    DateTimeOffset EndTime,
    string         Status,
    string?        VisitReason);
