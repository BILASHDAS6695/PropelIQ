using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class MarkNoShowCommandValidator : AbstractValidator<MarkNoShowCommand>
{
    public MarkNoShowCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();
    }
}
