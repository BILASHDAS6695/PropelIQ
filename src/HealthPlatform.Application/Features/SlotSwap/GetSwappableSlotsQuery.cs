using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Returns all booked appointment slots for a given provider that are eligible
/// for swap with the requester's appointment. The requester's own appointment
/// is excluded. Only slot times are returned — no patient identity is exposed.
/// </summary>
public sealed record GetSwappableSlotsQuery(
    Guid RequesterAppointmentId) : IRequest<IReadOnlyList<SwappableSlotDto>>;
