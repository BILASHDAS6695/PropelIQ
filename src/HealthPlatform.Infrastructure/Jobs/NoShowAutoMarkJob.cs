using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that automatically marks unchecked-in appointments
/// as NoShow after the 30-minute post-slot grace period has elapsed.
///
/// Runs on a minutely schedule.  For each eligible appointment it dispatches
/// <see cref="MarkNoShowCommand"/> with <c>IsAutomatic = true</c>, which
/// frees the slot, increments the patient's no-show counter, and sends the
/// follow-up email.  Failures for individual appointments are caught and
/// logged so a single bad record does not abort the entire batch.
/// </summary>
public sealed class NoShowAutoMarkJob
{
    private readonly IServiceScopeFactory       _scopeFactory;
    private readonly ILogger<NoShowAutoMarkJob> _logger;

    // 30-minute grace period after slot start before auto-marking.
    private static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(30);

    public NoShowAutoMarkJob(
        IServiceScopeFactory       scopeFactory,
        ILogger<NoShowAutoMarkJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>
    /// Entry point invoked by Hangfire on the configured cron schedule.
    /// Discovers all eligible appointments and dispatches
    /// <see cref="MarkNoShowCommand"/> for each one.
    /// </summary>
    public async Task ExecuteAsync(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var uow    = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var cutoff   = DateTimeOffset.UtcNow.Subtract(GracePeriod);
        var eligible = await uow.Repository<Appointment>()
            .GetAsync(new ActiveUnattendedPastCutoffSpecification(cutoff), ct);

        if (eligible.Count == 0)
            return;

        _logger.LogInformation(
            "NoShowAutoMarkJob: found {Count} appointment(s) eligible for auto no-show marking.",
            eligible.Count);

        foreach (var appointment in eligible)
        {
            try
            {
                await sender.Send(
                    new MarkNoShowCommand(appointment.Id, IsAutomatic: true), ct);

                _logger.LogInformation(
                    "NoShowAutoMarkJob: appointment {AppointmentId} marked NoShow (auto).",
                    appointment.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "NoShowAutoMarkJob: failed to mark appointment {AppointmentId} as NoShow.",
                    appointment.Id);
            }
        }
    }
}
