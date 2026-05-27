using MediatR;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns the full daily queue for a provider with multi-key sort and a
/// summary count block suitable for the dashboard header.
///
/// Sort order (all in-memory after DB retrieval):
///   1. InProgress — currently being seen (by ArrivalTime ASC)
///   2. Arrived    — checked in, waiting (by ArrivalTime ASC)
///   3. Scheduled / Booked — upcoming (by SlotTime ASC)
///   4. WalkIn     — unscheduled (by QueuePosition ASC)
/// </summary>
public sealed record GetProviderQueueDashboardQuery(Guid ProviderId, DateOnly Date)
    : IRequest<QueueDashboardDto>;

public sealed record QueueDashboardDto(
    IReadOnlyList<QueueEntryDto> Items,
    QueueSummaryDto              Summary);

/// <summary>
/// Counts for the dashboard header: "N waiting, N in progress, N remaining".
/// </summary>
public sealed record QueueSummaryDto(
    int Waiting,     // Arrived — checked in, not yet InProgress
    int InProgress,  // InProgress — currently being seen
    int Remaining);  // Scheduled + Booked + WalkIn — not yet arrived
