using FluentValidation;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class GetNoShowReportQueryValidator : AbstractValidator<GetNoShowReportQuery>
{
    public GetNoShowReportQueryValidator()
    {
        RuleFor(q => q.DateFrom).NotEmpty();
        RuleFor(q => q.DateTo)
            .NotEmpty()
            .GreaterThanOrEqualTo(q => q.DateFrom)
            .WithMessage("DateTo must be on or after DateFrom.");
        RuleFor(q => q)
            .Must(q => (q.DateTo.DayNumber - q.DateFrom.DayNumber) <= 90)
            .WithMessage("Report date range cannot exceed 90 days.");
    }
}
