using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class BookAppointmentCommandValidator
    : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentCommandValidator()
    {
        RuleFor(x => x.SlotId).NotEmpty();
        RuleFor(x => x.VisitReason)
            .MaximumLength(500)
            .When(x => x.VisitReason is not null);

        // OverrideReason is required when the caller sets ForceBook = true.
        RuleFor(c => c.OverrideReason)
            .NotEmpty()
            .WithMessage("An override reason is required when ForceBook is true.")
            .When(c => c.ForceBook);
    }
}
