using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class MarkPatientArrivedCommandValidator
    : AbstractValidator<MarkPatientArrivedCommand>
{
    public MarkPatientArrivedCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
