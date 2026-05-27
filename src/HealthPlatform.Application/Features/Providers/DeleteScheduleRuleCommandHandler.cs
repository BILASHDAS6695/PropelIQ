using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class DeleteScheduleRuleCommandHandler
    : IRequestHandler<DeleteScheduleRuleCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteScheduleRuleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task Handle(DeleteScheduleRuleCommand request, CancellationToken ct)
    {
        var rule = await _uow.Repository<ProviderScheduleRule>()
            .GetByIdAsync(request.RuleId, ct)
            ?? throw new KeyNotFoundException($"ScheduleRule {request.RuleId} not found.");

        _uow.Repository<ProviderScheduleRule>().Delete(rule);
        await _uow.SaveChangesAsync(ct);
    }
}
