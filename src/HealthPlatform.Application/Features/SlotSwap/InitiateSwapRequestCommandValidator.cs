using FluentValidation;

namespace HealthPlatform.Application.Features.SlotSwap;

public sealed class InitiateSwapRequestCommandValidator
    : AbstractValidator<InitiateSwapRequestCommand>
{
    public InitiateSwapRequestCommandValidator()
    {
        RuleFor(x => x.RequesterAppointmentId).NotEmpty();
        RuleFor(x => x.TargetAppointmentId).NotEmpty();

        RuleFor(x => x)
            .Must(x => x.RequesterAppointmentId != x.TargetAppointmentId)
            .WithMessage("Cannot initiate a swap request against your own appointment.")
            .WithName("TargetAppointmentId");
    }
}
