using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class TodayAppointmentsSearchQueryValidator
    : AbstractValidator<TodayAppointmentsSearchQuery>
{
    public TodayAppointmentsSearchQueryValidator()
    {
        // At least one filter must be provided to avoid a full-table scan
        RuleFor(q => q)
            .Must(q => q.ProviderId.HasValue
                    || !string.IsNullOrWhiteSpace(q.PatientNameFragment)
                    || q.AppointmentId.HasValue)
            .WithMessage("At least one search filter (ProviderId, PatientNameFragment, or AppointmentId) is required.");

        When(q => !string.IsNullOrWhiteSpace(q.PatientNameFragment), () =>
        {
            RuleFor(q => q.PatientNameFragment)
                .MinimumLength(2)
                .WithMessage("Patient name search requires at least 2 characters.");
        });
    }
}
