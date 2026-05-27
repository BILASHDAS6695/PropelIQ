using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds appointment slots for the next 30 days (9 AM–5 PM UTC, 30-minute
/// intervals per provider) on first startup when the slots table is empty.
/// Uses IServiceScopeFactory to resolve the scoped ApplicationDbContext
/// from a singleton/hosted-service context.
/// </summary>
internal sealed class AppointmentSlotSeedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AppointmentSlotSeedService> _logger;

    public AppointmentSlotSeedService(IServiceScopeFactory scopeFactory,
                                      ILogger<AppointmentSlotSeedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today   = DateTimeOffset.UtcNow.Date;
        var horizon = today.AddDays(30);

        var hasSlots = await db.AppointmentSlots
            .AnyAsync(s => s.StartTime >= today && s.StartTime < horizon,
                      stoppingToken);

        if (hasSlots)
        {
            _logger.LogInformation(
                "Appointment slots already seeded for the next 30 days. Skipping.");
            return;
        }

        var providerIds = await db.Providers
            .Where(p => !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(stoppingToken);

        if (providerIds.Count == 0)
        {
            _logger.LogWarning(
                "No providers found in database. Appointment slot seeding skipped.");
            return;
        }

        var slots = new List<AppointmentSlot>(providerIds.Count * 30 * 16);

        for (var day = 0; day < 30; day++)
        {
            var date = today.AddDays(day);

            foreach (var providerId in providerIds)
            {
                for (var hour = 9; hour < 17; hour++)
                {
                    for (var minute = 0; minute < 60; minute += 30)
                    {
                        var start = new DateTimeOffset(
                            date.Year, date.Month, date.Day,
                            hour, minute, 0,
                            TimeSpan.Zero);

                        slots.Add(new AppointmentSlot
                        {
                            Id         = Guid.NewGuid(),
                            ProviderId  = providerId,
                            StartTime   = start,
                            EndTime     = start.AddMinutes(30),
                            Status      = SlotStatus.Available
                        });
                    }
                }
            }
        }

        await db.AppointmentSlots.AddRangeAsync(slots, stoppingToken);
        await db.SaveChangesAsync(stoppingToken);

        _logger.LogInformation(
            "Seeded {Count} appointment slots across {Days} days for {Providers} providers.",
            slots.Count, 30, providerIds.Count);
    }
}
