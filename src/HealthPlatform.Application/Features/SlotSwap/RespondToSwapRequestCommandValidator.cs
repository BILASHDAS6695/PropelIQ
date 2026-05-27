using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class RespondToSwapRequestCommandValidator
    : AbstractValidator<RespondToSwapRequestCommand>
{
    public RespondToSwapRequestCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();

        RuleFor(c => c.Reason)
            .MaximumLength(500)
            .When(c => c.Reason is not null);
    }
}
