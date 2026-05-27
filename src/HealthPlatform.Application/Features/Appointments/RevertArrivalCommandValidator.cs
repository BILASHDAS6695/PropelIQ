using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class RevertArrivalCommandValidator
    : AbstractValidator<RevertArrivalCommand>
{
    public RevertArrivalCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
