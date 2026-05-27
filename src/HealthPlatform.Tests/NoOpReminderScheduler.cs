using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Tests;

/// <summary>
/// Test double for <see cref="IReminderScheduler"/> that does nothing.
/// Used in unit tests so handlers that require <see cref="IReminderScheduler"/>
/// can be exercised without a real Hangfire/database dependency.
/// </summary>
internal sealed class NoOpReminderScheduler : IReminderScheduler
{
    public Task ScheduleAsync(Appointment appointment, CancellationToken ct = default)
        => Task.CompletedTask;

    public void Cancel(Appointment appointment) { }
}
