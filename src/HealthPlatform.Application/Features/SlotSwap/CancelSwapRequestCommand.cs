using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Cancels a pending swap request initiated by the calling patient.
/// The caller's patient profile ID is resolved inside the handler from the
/// authenticated user's identity — never supplied by the request body.
/// </summary>
public sealed record CancelSwapRequestCommand(
    Guid    SwapRequestId,
    string? Reason = null) : IRequest;
