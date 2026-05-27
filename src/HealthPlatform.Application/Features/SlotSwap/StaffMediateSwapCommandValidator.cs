using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffMediateSwapCommandValidator
    : AbstractValidator<StaffMediateSwapCommand>
{
    public StaffMediateSwapCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for all staff override actions.")
            .MaximumLength(500);
    }
}
