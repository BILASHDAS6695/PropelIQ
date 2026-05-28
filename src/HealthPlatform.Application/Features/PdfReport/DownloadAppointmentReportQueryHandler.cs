using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.PdfReport;

internal sealed class DownloadAppointmentReportQueryHandler
    : IRequestHandler<DownloadAppointmentReportQuery, AppointmentReportFileDto>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public DownloadAppointmentReportQueryHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<AppointmentReportFileDto> Handle(
        DownloadAppointmentReportQuery query,
        CancellationToken              ct)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("User must be authenticated.");

        var reports = await _uow.Repository<Domain.Entities.PdfReport>()
            .GetAsync(new PdfReportByTokenSpecification(query.PatientProfileId, query.Token), ct);

        if (reports.Count == 0)
            throw new NotFoundException(nameof(Domain.Entities.PdfReport), query.Token);

        var report = reports[0];

        if (report.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new NotFoundException(
                nameof(Domain.Entities.PdfReport),
                "Report link has expired. Please request a new report.");

        if (report.Status != PdfReportStatus.Ready || report.FileBytes is null)
            throw new NotFoundException(
                nameof(Domain.Entities.PdfReport),
                "Report is still being generated. Please try again shortly.");

        var filename = $"appointments_{report.DateFrom:yyyyMMdd}_{report.DateTo:yyyyMMdd}.pdf";
        return new AppointmentReportFileDto(report.FileBytes, filename);
    }
}
