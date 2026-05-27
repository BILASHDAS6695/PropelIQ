using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record DeleteScheduleRuleCommand(Guid RuleId) : IRequest;
