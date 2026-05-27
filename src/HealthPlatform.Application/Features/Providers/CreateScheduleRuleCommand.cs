using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record CreateScheduleRuleCommand(
    Guid      ProviderId,
    DayOfWeek DayOfWeek,
    TimeOnly  StartTime,
    TimeOnly  EndTime,
    int       SlotDurationMinutes = 30) : IRequest<Guid>;
