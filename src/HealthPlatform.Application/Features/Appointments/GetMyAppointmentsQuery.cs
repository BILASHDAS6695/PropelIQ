using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>Returns all appointments belonging to the currently authenticated patient.</summary>
public sealed record GetMyAppointmentsQuery : IRequest<IReadOnlyList<PatientAppointmentDto>>;

public sealed record PatientAppointmentDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset SlotTime,
    DateTimeOffset EndTime,
    string         Status,
    string?        VisitReason,
    string         PatientName,
    string?        IntakeStatus,
    bool           IsIntakeWindowOpen);
