using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Pre-flight read-only conflict check for a proposed slot booking.
/// Returns the worst conflict severity for the authenticated patient against the
/// requested slot time.
///
/// The handler resolves the patient profile from <see cref="UserId"/> internally,
/// matching the same pattern used by <see cref="BookAppointmentCommandHandler"/>.
///
/// Severity values:
///   "None" — no conflicts; proceed with booking.
///   "Soft" — same day, different time (&gt; 30-min gap); warning, booking allowed.
///   "Hard" — time window overlap (within 30 min); booking blocked for patients,
///             overridable by Staff with a reason.
/// </summary>
public sealed record CheckAppointmentConflictsQuery(
    Guid UserId,
    Guid SlotId)
    : IRequest<ConflictCheckResultDto>;

public sealed record ConflictCheckResultDto(
    string          Severity,                   // "None" | "Soft" | "Hard"
    Guid?           ConflictingAppointmentId,
    string?         ConflictingProviderName,
    DateTimeOffset? ConflictingSlotTime,
    string?         Message);
