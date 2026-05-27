using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class RegisterWalkInCommandValidator
    : AbstractValidator<RegisterWalkInCommand>
{
    public RegisterWalkInCommandValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.VisitReason)
            .MaximumLength(500)
            .When(x => x.VisitReason is not null);
    }
}
