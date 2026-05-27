using FluentValidation;

namespace HealthPlatform.Application.Features.Providers;

public sealed class CreateScheduleRuleCommandValidator
    : AbstractValidator<CreateScheduleRuleCommand>
{
    public CreateScheduleRuleCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.EndTime)
            .GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be after StartTime.");
        RuleFor(x => x.SlotDurationMinutes)
            .InclusiveBetween(10, 120)
            .WithMessage("Slot duration must be between 10 and 120 minutes.");
    }
}
