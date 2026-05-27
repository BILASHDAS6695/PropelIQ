using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Persistence.Seed;

/// <summary>
/// Generates appointment slots for the next 90 days (rolling window) at
/// startup, driven by <see cref="ProviderScheduleRule"/> records.
/// Falls back to a default 09:00–17:00 window with 30-minute slots when no
/// rules are configured for a provider/day combination.
/// Skips days in <see cref="ProviderUnavailability"/> and slots that already
/// exist (idempotent). Never modifies or deletes Booked slots — schedule
/// changes do not affect already-booked appointments.
/// </summary>
internal sealed class SlotGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory           _scopeFactory;
    private readonly ILogger<SlotGenerationService> _logger;

    private static readonly TimeOnly DefaultStart    = new(9,  0);
    private static readonly TimeOnly DefaultEnd      = new(17, 0);
    private const           int      DefaultDuration = 30;
    private const           int      HorizonDays     = 90;

    public SlotGenerationService(
        IServiceScopeFactory           scopeFactory,
        ILogger<SlotGenerationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today   = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(HorizonDays);

        var providerIds = await db.Providers
            .Where(p => !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(stoppingToken);

        if (providerIds.Count == 0)
        {
            _logger.LogWarning("SlotGenerationService: no providers found. Slot generation skipped.");
            return;
        }

        // Load all active schedule rules grouped by provider.
        var rulesByProvider = await db.ProviderScheduleRules
            .Where(r => !r.IsDeleted)
            .ToListAsync(stoppingToken);

        var ruleMap = rulesByProvider
            .GroupBy(r => r.ProviderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load unavailabilities within the generation horizon.
        var unavailabilities = await db.ProviderUnavailabilities
            .Where(u => !u.IsDeleted
                     && u.UnavailableDate >= today
                     && u.UnavailableDate <= horizon)
            .Select(u => new { u.ProviderId, u.UnavailableDate })
            .ToListAsync(stoppingToken);

        // Build a fast lookup set: (providerId, date).
        var unavailableSet = unavailabilities
            .Select(u => (u.ProviderId, u.UnavailableDate))
            .ToHashSet();

        // Load existing slot start-times to make generation idempotent.
        var horizonOffset = new DateTimeOffset(
            today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);

        var existingSet = (await db.AppointmentSlots
            .Where(s => s.StartTime >= horizonOffset)
            .Select(s => new { s.ProviderId, s.StartTime })
            .ToListAsync(stoppingToken))
            .Select(x => (x.ProviderId, x.StartTime))
            .ToHashSet();

        var newSlots  = new List<AppointmentSlot>();
        var daysAdded = 0;

        for (var dayOffset = 0; dayOffset < HorizonDays; dayOffset++)
        {
            var date = today.AddDays(dayOffset);

            foreach (var providerId in providerIds)
            {
                // Skip dates blocked by provider unavailability.
                if (unavailableSet.Contains((providerId, date)))
                    continue;

                // Resolve schedule rule for this provider + day-of-week.
                TimeOnly start, end;
                int      duration;

                if (ruleMap.TryGetValue(providerId, out var rules))
                {
                    var dayRule = rules.FirstOrDefault(r => r.DayOfWeek == date.DayOfWeek);
                    if (dayRule is not null)
                    {
                        start    = dayRule.StartTime;
                        end      = dayRule.EndTime;
                        duration = dayRule.SlotDurationMinutes;
                    }
                    else
                    {
                        // Provider has rules but none cover this day — skip.
                        continue;
                    }
                }
                else
                {
                    // No rules configured: fall back to default 09:00–17:00 window.
                    start    = DefaultStart;
                    end      = DefaultEnd;
                    duration = DefaultDuration;
                }

                var current = start;
                while (current.AddMinutes(duration) <= end)
                {
                    var slotStart = new DateTimeOffset(
                        date.Year, date.Month, date.Day,
                        current.Hour, current.Minute, 0,
                        TimeSpan.Zero);

                    if (!existingSet.Contains((providerId, slotStart)))
                    {
                        newSlots.Add(new AppointmentSlot
                        {
                            Id         = Guid.NewGuid(),
                            ProviderId = providerId,
                            StartTime  = slotStart,
                            EndTime    = slotStart.AddMinutes(duration),
                            Status     = SlotStatus.Available
                        });
                    }

                    current = current.AddMinutes(duration);
                }
            }

            daysAdded++;
        }

        if (newSlots.Count > 0)
        {
            await db.AppointmentSlots.AddRangeAsync(newSlots, stoppingToken);
            await db.SaveChangesAsync(stoppingToken);
            _logger.LogInformation(
                "SlotGenerationService: generated {Count} slots across {Days} days.",
                newSlots.Count, daysAdded);
        }
        else
        {
            _logger.LogInformation(
                "SlotGenerationService: all slots up to date for the next {Days} days.",
                HorizonDays);
        }
    }
}
