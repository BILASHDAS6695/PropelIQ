using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Marks a single appointment as <see cref="Domain.Enums.AppointmentStatus.NoShow"/>.
///
/// <paramref name="IsAutomatic"/> distinguishes Hangfire auto-marking
/// (no authentication context) from manual staff action (authenticated).
/// The distinction is captured in the audit log via the modified entity fields.
/// </summary>
public sealed record MarkNoShowCommand(
    Guid AppointmentId,
    bool IsAutomatic = false) : IRequest<NoShowConfirmationDto>;

/// <summary>
/// Returned by <see cref="MarkNoShowCommand"/> so the API controller can
/// broadcast a SignalR notification to the relevant provider group.
/// </summary>
public sealed record NoShowConfirmationDto(
    Guid           AppointmentId,
    Guid           PatientId,
    Guid           ProviderId,
    DateTimeOffset SlotTime,
    bool           IsAutomatic,
    int            PatientTotalNoShowCount);
