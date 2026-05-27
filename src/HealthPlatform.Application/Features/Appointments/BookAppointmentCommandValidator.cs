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
    }
}
