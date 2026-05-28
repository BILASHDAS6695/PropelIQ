using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns today's appointments optionally scoped to one provider and filtered
/// by a patient name fragment (case-insensitive partial match) or an exact
/// appointment ID.  Intended for the front-desk staff check-in search screen.
/// </summary>
public sealed record TodayAppointmentsSearchQuery(
    Guid?   ProviderId,
    string? PatientNameFragment,
    Guid?   AppointmentId,
    bool?   HasIntakePending = null)
    : IRequest<IReadOnlyList<TodayAppointmentItemDto>>;

public sealed record TodayAppointmentItemDto(
    Guid            AppointmentId,
    Guid            PatientId,
    string          PatientFullName,
    string          Status,
    DateTimeOffset  SlotTime,
    bool            IsWalkIn,
    bool            IsLateArrival,
    DateTimeOffset? ArrivalTime,
    string?         IntakeStatus);
