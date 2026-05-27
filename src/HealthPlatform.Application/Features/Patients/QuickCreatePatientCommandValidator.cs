using FluentValidation;

namespace HealthPlatform.Application.Features.Patients;

public sealed class QuickCreatePatientCommandValidator
    : AbstractValidator<QuickCreatePatientCommand>
{
    public QuickCreatePatientCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Dob).NotEmpty()
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Phone)
            .MaximumLength(20)
            .When(x => x.Phone is not null);
    }
}
