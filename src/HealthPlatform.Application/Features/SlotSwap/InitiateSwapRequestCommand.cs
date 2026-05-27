using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Initiates a slot swap request: the caller offers their current appointment
/// slot in exchange for the target appointment's slot.
/// The caller's patient profile ID is resolved inside the handler from the
/// authenticated user's identity — never supplied by the request body.
/// </summary>
public sealed record InitiateSwapRequestCommand(
    Guid RequesterAppointmentId,
    Guid TargetAppointmentId) : IRequest<SwapRequestDto>;
