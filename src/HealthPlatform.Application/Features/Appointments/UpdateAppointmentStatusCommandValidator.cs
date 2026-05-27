using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class UpdateAppointmentStatusCommandValidator
    : AbstractValidator<UpdateAppointmentStatusCommand>
{
    private static readonly string[] AllowedTargetStatuses = ["InProgress", "Completed"];

    public UpdateAppointmentStatusCommandValidator()
    {
        RuleFor(c => c.AppointmentId).NotEmpty();

        RuleFor(c => c.NewStatus)
            .NotEmpty()
            .Must(s => AllowedTargetStatuses.Contains(s, StringComparer.OrdinalIgnoreCase))
            .WithMessage(
                $"Allowed target statuses are: {string.Join(", ", AllowedTargetStatuses)}.");
    }
}
