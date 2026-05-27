using FluentValidation;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class CancelAppointmentCommandValidator
    : AbstractValidator<CancelAppointmentCommand>
{
    public CancelAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage("Cancellation reason must be one of: ScheduleConflict, FeelingBetter, Other.");

        // Note is required in the UI when Reason = Other; enforce it here too.
        RuleFor(x => x.Note)
            .NotEmpty()
            .WithMessage("A cancellation note is required when the reason is Other.")
            .When(x => x.Reason == CancellationReason.Other);
    }
}
