using FluentValidation;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.Appointments;

public sealed class RescheduleAppointmentCommandValidator
    : AbstractValidator<RescheduleAppointmentCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.AppointmentId).NotEmpty();
        RuleFor(x => x.NewSlotId).NotEmpty();

        RuleFor(x => x.Reason)
            .IsInEnum()
            .WithMessage("Cancellation reason must be one of: ScheduleConflict, FeelingBetter, Other.");

        RuleFor(x => x.Note)
            .NotEmpty()
            .WithMessage("A note is required when the reason is Other.")
            .When(x => x.Reason == CancellationReason.Other);
    }
}
