using Hangfire;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.Patients;
using HealthPlatform.Application.Features.PdfReport;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Reports;

/// <summary>
/// Hangfire background job that generates a PDF report asynchronously
/// for report requests with more than 50 appointments.
/// </summary>
public sealed class GeneratePdfReportJob
{
    private readonly IUnitOfWork                    _uow;
    private readonly IAppointmentReportBuilder      _builder;
    private readonly ILogger<GeneratePdfReportJob>  _logger;

    public GeneratePdfReportJob(
        IUnitOfWork                    uow,
        IAppointmentReportBuilder      builder,
        ILogger<GeneratePdfReportJob>  logger)
    {
        _uow     = uow;
        _builder = builder;
        _logger  = logger;
    }

    [AutomaticRetry(Attempts = 2, DelaysInSeconds = new[] { 60, 300 })]
    public async Task ExecuteAsync(Guid pdfReportId, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GeneratePdfReportJob: starting report {ReportId}", pdfReportId);

        var reportResults = await _uow.Repository<Domain.Entities.PdfReport>()
            .GetAsync(new PdfReportByIdSpecification(pdfReportId), ct);

        if (reportResults.Count == 0)
        {
            _logger.LogWarning(
                "GeneratePdfReportJob: report {ReportId} not found — skipping.", pdfReportId);
            return;
        }

        var report = reportResults[0];

        try
        {
            var profiles = await _uow.Repository<Domain.Entities.PatientProfile>()
                .GetAsync(new PatientProfileByIdSpecification(report.PatientId), ct);

            if (profiles.Count == 0)
            {
                report.Status = PdfReportStatus.Failed;
                await _uow.SaveChangesAsync(ct);
                return;
            }

            var profile = profiles[0];

            var appointments = await _uow.Repository<Domain.Entities.Appointment>()
                .GetAsync(
                    new AppointmentsForReportSpecification(
                        report.PatientId, report.DateFrom, report.DateTo),
                    ct);

            var data = new AppointmentReportData(
                $"{profile.FirstName} {profile.LastName}",
                report.DateFrom,
                report.DateTo,
                appointments
                    .Select(a => new AppointmentReportRow(
                        a.SlotTime,
                        a.Provider.Name,
                        a.Status.ToString(),
                        a.VisitReason))
                    .ToList());

            report.FileBytes        = _builder.Build(data);
            report.Status           = PdfReportStatus.Ready;
            report.AppointmentCount = appointments.Count;

            await _uow.SaveChangesAsync(ct);

            _logger.LogInformation(
                "GeneratePdfReportJob: report {ReportId} complete ({Count} appointments).",
                pdfReportId, appointments.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GeneratePdfReportJob: failed to generate report {ReportId}.", pdfReportId);
            report.Status = PdfReportStatus.Failed;
            await _uow.SaveChangesAsync(ct);
            throw;   // re-throw so Hangfire marks the job as failed and retries
        }
    }
}
