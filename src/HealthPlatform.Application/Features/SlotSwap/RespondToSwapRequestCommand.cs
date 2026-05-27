using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Issued by the target patient to accept or decline a pending swap request.
/// </summary>
/// <param name="SwapRequestId">ID of the swap request to respond to.</param>
/// <param name="Accept">
///   <c>true</c> to accept (slots are swapped); <c>false</c> to decline.
/// </param>
/// <param name="Reason">Optional decline reason. Ignored on accept.</param>
public sealed record RespondToSwapRequestCommand(
    Guid    SwapRequestId,
    bool    Accept,
    string? Reason = null) : IRequest<SwapResponseDto>;
