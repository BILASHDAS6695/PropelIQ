using Hangfire;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using HealthPlatform.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Hangfire job that sends an appointment reminder email.
/// Idempotent: if the appointment is already Cancelled, Completed, or NoShow
/// at execution time the job logs and exits without sending.
/// Retried up to 3 times with exponential back-off on transient failures.
/// </summary>
internal sealed class AppointmentReminderJob
{
    private readonly IUnitOfWork                     _uow;
    private readonly IEmailSender                    _emailSender;
    private readonly ILogger<AppointmentReminderJob> _logger;

    public AppointmentReminderJob(
        IUnitOfWork                     uow,
        IEmailSender                    emailSender,
        ILogger<AppointmentReminderJob> logger)
    {
        _uow         = uow;
        _emailSender = emailSender;
        _logger      = logger;
    }

    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 1500, 7500 })]
    public async Task ExecuteAsync(Guid appointmentId, CancellationToken ct = default)
    {
        var results = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentForReminderSpecification(appointmentId), ct);

        if (results.Count == 0)
        {
            _logger.LogWarning(
                "AppointmentReminderJob: appointment {AppointmentId} not found — skipping.",
                appointmentId);
            return;
        }

        var appointment = results[0];

        // Idempotency guard — skip terminal states
        if (appointment.Status is AppointmentStatus.Cancelled
                                or AppointmentStatus.Completed
                                or AppointmentStatus.NoShow)
        {
            _logger.LogInformation(
                "AppointmentReminderJob: appointment {AppointmentId} is {Status} — skipping reminder.",
                appointmentId,
                appointment.Status);
            return;
        }

        var patientName  = $"{appointment.Patient.FirstName} {appointment.Patient.LastName}";
        var providerName = appointment.Provider.Name;
        var email        = appointment.Patient.User.Email;

        var (subject, body) = EmailTemplateService.Reminder(
            patientName,
            providerName,
            appointment.SlotTime,
            appointment.Id);

        _logger.LogInformation(
            "AppointmentReminderJob: sending reminder for appointment {AppointmentId} to {Email}.",
            appointmentId,
            email);

        await _emailSender.SendAsync(email, subject, body, ct);
    }
}
