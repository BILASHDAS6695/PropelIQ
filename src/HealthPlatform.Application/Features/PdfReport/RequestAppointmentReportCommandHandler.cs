using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Features.Patients;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.PdfReport;

internal sealed class RequestAppointmentReportCommandHandler
    : IRequestHandler<RequestAppointmentReportCommand, AppointmentReportResponseDto>
{
    private const int AsyncThreshold   = 50;
    private const int ReportExpiryMins = 60;

    private readonly IUnitOfWork                _uow;
    private readonly ICurrentUserService        _currentUser;
    private readonly IAppointmentReportBuilder  _builder;
    private readonly IReportJobScheduler        _jobs;

    public RequestAppointmentReportCommandHandler(
        IUnitOfWork               uow,
        ICurrentUserService       currentUser,
        IAppointmentReportBuilder builder,
        IReportJobScheduler       jobs)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _builder     = builder;
        _jobs        = jobs;
    }

    public async Task<AppointmentReportResponseDto> Handle(
        RequestAppointmentReportCommand command,
        CancellationToken               ct)
    {
        // ── 1. Ownership check ─────────────────────────────────────────────
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByIdSpecification(command.PatientProfileId), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), command.PatientProfileId);

        var profile = profiles[0];

        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException("User must be authenticated.");

        var callerUserId = _currentUser.UserId!.Value;

        var callerUsers = await _uow.Repository<User>()
            .GetAsync(new UserByIdSpecification(callerUserId), ct);

        var isStaffOrAdmin = callerUsers.Count > 0
            && callerUsers[0].Role is UserRole.Staff or UserRole.Admin;

        if (!isStaffOrAdmin && profile.UserId != callerUserId)
            throw new ForbiddenAccessException(
                "Patients may only generate their own appointment report.");

        // ── 2. Deduplication — return an existing valid report ─────────────
        var now = DateTimeOffset.UtcNow;

        var existingReports = await _uow.Repository<Domain.Entities.PdfReport>()
            .GetAsync(
                new ExistingValidPdfReportSpecification(
                    command.PatientProfileId, command.From, command.To),
                ct);

        if (existingReports.Count > 0)
        {
            var existing = existingReports[0];
            return new AppointmentReportResponseDto(
                existing.Token,
                BuildDownloadUrl(command.PatientProfileId, existing.Token),
                existing.ExpiresAt,
                existing.Status == PdfReportStatus.Ready);
        }

        // ── 3. Load appointments (capped at 100 by the spec) ──────────────
        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(
                new AppointmentsForReportSpecification(
                    command.PatientProfileId, command.From, command.To),
                ct);

        // ── 4. Create the PdfReport record ────────────────────────────────
        var report = new Domain.Entities.PdfReport
        {
            PatientId        = command.PatientProfileId,
            Token            = Guid.NewGuid(),
            DateFrom         = command.From,
            DateTo           = command.To,
            Status           = PdfReportStatus.Pending,
            ExpiresAt        = now.AddMinutes(ReportExpiryMins),
            AppointmentCount = appointments.Count,
        };

        await _uow.Repository<Domain.Entities.PdfReport>().AddAsync(report, ct);

        // ── 5. Sync path (≤ AsyncThreshold) ──────────────────────────────
        if (appointments.Count <= AsyncThreshold)
        {
            var data = BuildReportData(profile, command.From, command.To, appointments);
            report.FileBytes = _builder.Build(data);
            report.Status    = PdfReportStatus.Ready;
        }

        await _uow.SaveChangesAsync(ct);

        // ── 6. Async path (> AsyncThreshold) — enqueue after save ─────────
        if (appointments.Count > AsyncThreshold)
        {
            _jobs.EnqueueGenerate(report.Id);
        }

        return new AppointmentReportResponseDto(
            report.Token,
            BuildDownloadUrl(command.PatientProfileId, report.Token),
            report.ExpiresAt,
            report.Status == PdfReportStatus.Ready);
    }

    private static AppointmentReportData BuildReportData(
        PatientProfile             profile,
        DateTimeOffset             from,
        DateTimeOffset             to,
        IReadOnlyList<Appointment> appointments)
    {
        var rows = appointments
            .Select(a => new AppointmentReportRow(
                a.SlotTime,
                a.Provider.Name,
                a.Status.ToString(),
                a.VisitReason))
            .ToList();

        return new AppointmentReportData(
            $"{profile.FirstName} {profile.LastName}",
            from,
            to,
            rows);
    }

    private static string BuildDownloadUrl(Guid patientProfileId, Guid token) =>
        $"/api/patients/{patientProfileId}/appointments/pdf/download?token={token}";
}
