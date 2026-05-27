using FluentValidation;

namespace HealthPlatform.Application.Features.Providers;

public sealed class CreateUnavailabilityCommandValidator
    : AbstractValidator<CreateUnavailabilityCommand>
{
    public CreateUnavailabilityCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.UnavailableDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("UnavailableDate cannot be in the past.");
        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => x.Reason is not null);
    }
}
