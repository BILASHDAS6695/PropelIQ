using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class CreateScheduleRuleCommandHandler
    : IRequestHandler<CreateScheduleRuleCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public CreateScheduleRuleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(
        CreateScheduleRuleCommand request,
        CancellationToken         ct)
    {
        var repo = _uow.Repository<ProviderScheduleRule>();

        // Reject if a rule already exists for this provider + day-of-week combination.
        var existing = await repo.GetAsync(
            new ScheduleRuleByProviderAndDaySpecification(
                request.ProviderId, request.DayOfWeek), ct);

        if (existing.Count > 0)
            throw new InvalidOperationException(
                $"A schedule rule for {request.DayOfWeek} already exists for this provider. " +
                "Delete the existing rule before creating a new one.");

        var rule = new ProviderScheduleRule
        {
            Id                  = Guid.NewGuid(),
            ProviderId          = request.ProviderId,
            DayOfWeek           = request.DayOfWeek,
            StartTime           = request.StartTime,
            EndTime             = request.EndTime,
            SlotDurationMinutes = request.SlotDurationMinutes
        };

        await repo.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);
        return rule.Id;
    }
}
