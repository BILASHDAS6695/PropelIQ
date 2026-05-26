# Task 003: Dynamic Appointment Slot Seeding and Concrete Specification

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-012 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure (IHostedService + Specification) + API (DI registration) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (IUnitOfWork, BaseSpecification); Task 002 (providers must be seeded first) |

## Objective

Appointment slots are date-relative (next 30 days from today) so they cannot
be expressed as static `HasData` rows. A `BackgroundService` seeds 30 days ×
16 slots/day × 5 providers = **2,400 slots** on first startup when the table
is empty. A concrete `AvailableSlotsByProviderAndDateRangeSpecification`
demonstrates the specification pattern for the most common slot-query use case.

## Acceptance Criteria Covered

- AC-3: Seed appointment slots for the next 30 days (9 AM–5 PM, 30-min slots per provider)
- AC-7 (example): `AvailableSlotsByProviderAndDateRangeSpecification` as a specification usage example
- AC-8: Seed data runs automatically on startup if table is empty

## Implementation Steps

### 1. Create `AvailableSlotsByProviderAndDateRangeSpecification`

Create `src/HealthPlatform.Infrastructure/Persistence/Specifications/AvailableSlotsByProviderAndDateRangeSpecification.cs`:

```csharp
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

/// <summary>
/// Returns available appointment slots for a given provider within a
/// date/time window, ordered by start time ascending.
/// Usage: new AvailableSlotsByProviderAndDateRangeSpecification(providerId, from, to)
/// </summary>
public sealed class AvailableSlotsByProviderAndDateRangeSpecification
    : BaseSpecification<AppointmentSlot>
{
    public AvailableSlotsByProviderAndDateRangeSpecification(
        Guid            providerId,
        DateTimeOffset  from,
        DateTimeOffset  to)
        : base(s => s.ProviderId == providerId
                 && s.IsAvailable
                 && s.StartTime >= from
                 && s.StartTime < to)
    {
        ApplyOrderBy(s => s.StartTime);
    }
}
```

### 2. Create `AppointmentSlotSeedService`

Create `src/HealthPlatform.Infrastructure/Persistence/Seed/AppointmentSlotSeedService.cs`:

```csharp
using HealthPlatform.Domain.Entities;
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
                            Id          = Guid.NewGuid(),
                            ProviderId  = providerId,
                            StartTime   = start,
                            EndTime     = start.AddMinutes(30),
                            IsAvailable = true
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
```

### 3. Register `AppointmentSlotSeedService` in `DependencyInjection.cs`

Add after the `IUnitOfWork` registration:

```csharp
services.AddHostedService<AppointmentSlotSeedService>();
```

### 4. Add `using` for the Seed namespace in `DependencyInjection.cs`

```csharp
using HealthPlatform.Infrastructure.Persistence.Seed;
```

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/AvailableSlotsByProviderAndDateRangeSpecification.cs` | New — concrete specification for available slot queries |
| `src/HealthPlatform.Infrastructure/Persistence/Seed/AppointmentSlotSeedService.cs` | New — hosted service that seeds 2,400 slots on first startup |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `AppointmentSlotSeedService` + add using |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

**Integration test (manual — requires running Postgres + seeded providers):**
```bash
# Start the API, then verify via health endpoint and logs:
# Expected log: "Seeded 2400 appointment slots across 30 days for 5 providers."
# On second startup: "Appointment slots already seeded for the next 30 days. Skipping."
```

## Notes

- `BackgroundService.ExecuteAsync` is called once per application lifetime,
  immediately after the host starts — appropriate for idempotent startup seeding.
- `IServiceScopeFactory.CreateAsyncScope()` is required because `ApplicationDbContext`
  is `Scoped` while `BackgroundService` is effectively `Singleton`. Without
  the scope, DI would throw a lifetime violation at startup.
- The idempotency guard (`AnyAsync` on the date window) prevents duplicate
  slots on restarts without needing a separate migration flag column.
- Slots use `Guid.NewGuid()` (not deterministic IDs) because they are
  date-relative and will be recreated each deployment window. Static GUIDs
  are reserved for `HasData` entities (providers, insurance records).
- `9 AM–5 PM` UTC maps to 8 × 2 = 16 slots/hour-block/provider. Total:
  5 providers × 30 days × 16 slots = 2,400 slots — well within Postgres
  bulk-insert performance limits for a startup operation.
- `AvailableSlotsByProviderAndDateRangeSpecification` is `public sealed`
  because Application-layer command/query handlers outside Infrastructure
  will instantiate it by name. All other spec/repo/uow types remain `internal`.
