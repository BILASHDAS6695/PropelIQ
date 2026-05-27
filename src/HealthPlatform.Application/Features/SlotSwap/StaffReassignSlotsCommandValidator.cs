using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

internal sealed class StaffReassignSlotsCommandValidator
    : AbstractValidator<StaffReassignSlotsCommand>
{
    public StaffReassignSlotsCommandValidator()
    {
        RuleFor(c => c.SwapRequestId).NotEmpty();
        RuleFor(c => c.NewTargetSlotId).NotEmpty();

        RuleFor(c => c.Reason)
            .NotEmpty()
            .WithMessage("A reason is required for all staff override actions.")
            .MaximumLength(500);
    }
}
