using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Reminders;

/// <summary>
/// Schedules or cancels Hangfire reminder jobs for a given appointment.
/// Jobs that would fire in the past are silently skipped (e.g. appointment
/// booked &lt; 24 h from now skips the 24 h reminder).
/// </summary>
internal sealed class HangfireReminderScheduler : IReminderScheduler
{
    private readonly IBackgroundJobClient               _jobs;
    private readonly IUnitOfWork                        _uow;
    private readonly ReminderSettings                   _settings;
    private readonly ILogger<HangfireReminderScheduler> _logger;

    public HangfireReminderScheduler(
        IBackgroundJobClient               jobs,
        IUnitOfWork                        uow,
        IOptions<ReminderSettings>         settings,
        ILogger<HangfireReminderScheduler> logger)
    {
        _jobs     = jobs;
        _uow      = uow;
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task ScheduleAsync(Appointment appointment, CancellationToken ct = default)
    {
        var now      = DateTimeOffset.UtcNow;
        var slotTime = appointment.SlotTime;

        // ── First reminder (default: 24 h before) ────────────────────────
        var trigger24h = slotTime.AddHours(-_settings.HoursBeforeFirst);
        if (trigger24h > now)
        {
            appointment.Reminder24hJobId = _jobs.Schedule<AppointmentReminderJob>(
                job => job.ExecuteAsync(appointment.Id, CancellationToken.None),
                trigger24h);

            _logger.LogInformation(
                "Reminder [{HoursBeforeFirst}h] scheduled for appointment {AppointmentId} at {TriggerTime}.",
                _settings.HoursBeforeFirst,
                appointment.Id,
                trigger24h);
        }
        else
        {
            _logger.LogDebug(
                "Reminder [{HoursBeforeFirst}h] skipped for appointment {AppointmentId} — trigger {TriggerTime} is in the past.",
                _settings.HoursBeforeFirst,
                appointment.Id,
                trigger24h);
        }

        // ── Second reminder (default: 2 h before) ────────────────────────
        var trigger2h = slotTime.AddHours(-_settings.HoursBeforeSecond);
        if (trigger2h > now)
        {
            appointment.Reminder2hJobId = _jobs.Schedule<AppointmentReminderJob>(
                job => job.ExecuteAsync(appointment.Id, CancellationToken.None),
                trigger2h);

            _logger.LogInformation(
                "Reminder [{HoursBeforeSecond}h] scheduled for appointment {AppointmentId} at {TriggerTime}.",
                _settings.HoursBeforeSecond,
                appointment.Id,
                trigger2h);
        }
        else
        {
            _logger.LogDebug(
                "Reminder [{HoursBeforeSecond}h] skipped for appointment {AppointmentId} — trigger {TriggerTime} is in the past.",
                _settings.HoursBeforeSecond,
                appointment.Id,
                trigger2h);
        }

        _uow.Repository<Appointment>().Update(appointment);
        await _uow.SaveChangesAsync(ct);
    }

    public void Cancel(Appointment appointment)
    {
        if (appointment.Reminder24hJobId is not null)
        {
            _jobs.Delete(appointment.Reminder24hJobId);
            appointment.Reminder24hJobId = null;
            _logger.LogInformation(
                "Reminder [24h] job deleted for appointment {AppointmentId}.",
                appointment.Id);
        }

        if (appointment.Reminder2hJobId is not null)
        {
            _jobs.Delete(appointment.Reminder2hJobId);
            appointment.Reminder2hJobId = null;
            _logger.LogInformation(
                "Reminder [2h] job deleted for appointment {AppointmentId}.",
                appointment.Id);
        }
    }
}
