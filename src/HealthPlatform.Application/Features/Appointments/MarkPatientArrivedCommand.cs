using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Marks a booked appointment as Arrived and stamps the check-in timestamp.
/// Staff/Admin only — enforced at the controller level via [Authorize(Policy = PolicyNames.Staff)].
/// </summary>
public sealed record MarkPatientArrivedCommand(Guid AppointmentId)
    : IRequest<ArrivalConfirmationDto>;

/// <summary>
/// Returned to the API controller so it can broadcast a SignalR notification
/// to the provider's dashboard group without the handler depending on SignalR.
/// </summary>
public sealed record ArrivalConfirmationDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    Guid           PatientId,
    string         PatientFullName,
    DateTimeOffset ArrivalTime,
    bool           IsLateArrival);
