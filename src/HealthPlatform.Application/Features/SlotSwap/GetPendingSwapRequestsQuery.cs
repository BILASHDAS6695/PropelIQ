using MediatR;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Staff query: retrieves all pending slot swap requests with both patient names visible.
/// </summary>
public sealed record GetPendingSwapRequestsQuery
    : IRequest<IReadOnlyList<PendingSwapRequestSummaryDto>>;
