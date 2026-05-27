using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Schedules or cancels Hangfire reminder jobs for a given appointment.
/// Implementations live in the Infrastructure layer and interact with
/// <see cref="Hangfire.IBackgroundJobClient"/> directly.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>
    /// Enqueues the configured reminder jobs (default: 24 h and 2 h before
    /// slot time) for <paramref name="appointment"/>.  Jobs that would fire
    /// in the past are silently skipped.  Persists the returned Hangfire job
    /// IDs back onto the entity and saves via <see cref="IUnitOfWork"/>.
    /// </summary>
    Task ScheduleAsync(Appointment appointment, CancellationToken ct = default);

    /// <summary>
    /// Deletes any pending reminder jobs from Hangfire and nulls the job-ID
    /// fields on <paramref name="appointment"/>.  Does <em>not</em> call
    /// <see cref="IUnitOfWork.SaveChangesAsync"/> — the calling handler is
    /// responsible for the final save so that job-ID nullification is batched
    /// with the status-change mutation.
    /// </summary>
    void Cancel(Appointment appointment);
}
