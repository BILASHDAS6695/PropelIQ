# Task 002: Application Layer — CQRS Commands, Queries & Slot Generation

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-019 |
| **Epic** | EP-002 |
| **Layer** | Application (CQRS) + Infrastructure (slot generation service + specification) |
| **Priority** | Critical |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (domain model + migration must be applied) |

## Objective

Implement MediatR commands and queries for schedule-rule management, date
blocking, and slot retrieval. Replace the hardcoded seed service with a
rule-driven slot generation background service that covers a rolling 90-day
window and respects provider unavailabilities. Add a specification for
fetching slots by provider and date.

## Acceptance Criteria Covered

- AC: System auto-generates individual appointment slots from schedule rules
- AC: Slots generated for next 90 days (rolling window)
- AC: Schedule changes do not affect already-booked slots
- AC: `GET /providers/{id}/slots?date={date}` returns available slots (query side)
- AC: Overlapping schedule rules → reject at creation time

---

## Implementation Steps

### 1. `CreateScheduleRuleCommand` + Handler + Validator

Create `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record CreateScheduleRuleCommand(
    Guid      ProviderId,
    DayOfWeek DayOfWeek,
    TimeOnly  StartTime,
    TimeOnly  EndTime,
    int       SlotDurationMinutes = 30) : IRequest<Guid>;
```

Create `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Providers;

public sealed class CreateScheduleRuleCommandValidator
    : AbstractValidator<CreateScheduleRuleCommand>
{
    public CreateScheduleRuleCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.EndTime).GreaterThan(x => x.StartTime)
            .WithMessage("EndTime must be after StartTime.");
        RuleFor(x => x.SlotDurationMinutes)
            .InclusiveBetween(10, 120)
            .WithMessage("Slot duration must be between 10 and 120 minutes.");
    }
}
```

Create `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class CreateScheduleRuleCommandHandler
    : IRequestHandler<CreateScheduleRuleCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public CreateScheduleRuleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(
        CreateScheduleRuleCommand request,
        CancellationToken         ct)
    {
        var repo = _uow.Repository<ProviderScheduleRule>();

        // Check for duplicate day-of-week rule for this provider
        var existing = await repo.GetAsync(
            new ScheduleRuleByProviderAndDaySpecification(
                request.ProviderId, request.DayOfWeek), ct);

        if (existing.Count > 0)
            throw new InvalidOperationException(
                $"A schedule rule for {request.DayOfWeek} already exists for this provider. " +
                "Delete the existing rule before creating a new one.");

        var rule = new ProviderScheduleRule
        {
            Id                  = Guid.NewGuid(),
            ProviderId          = request.ProviderId,
            DayOfWeek           = request.DayOfWeek,
            StartTime           = request.StartTime,
            EndTime             = request.EndTime,
            SlotDurationMinutes = request.SlotDurationMinutes
        };

        await repo.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);
        return rule.Id;
    }
}
```

---

### 2. `DeleteScheduleRuleCommand` + Handler

Create `src/HealthPlatform.Application/Features/Providers/DeleteScheduleRuleCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record DeleteScheduleRuleCommand(Guid RuleId) : IRequest;
```

Create `src/HealthPlatform.Application/Features/Providers/DeleteScheduleRuleCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class DeleteScheduleRuleCommandHandler
    : IRequestHandler<DeleteScheduleRuleCommand>
{
    private readonly IUnitOfWork _uow;

    public DeleteScheduleRuleCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task Handle(DeleteScheduleRuleCommand request, CancellationToken ct)
    {
        var rule = await _uow.Repository<ProviderScheduleRule>()
            .GetByIdAsync(request.RuleId, ct)
            ?? throw new KeyNotFoundException($"ScheduleRule {request.RuleId} not found.");

        _uow.Repository<ProviderScheduleRule>().Delete(rule);
        await _uow.SaveChangesAsync(ct);
    }
}
```

---

### 3. `CreateUnavailabilityCommand` + Handler + Validator

Create `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommand.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record CreateUnavailabilityCommand(
    Guid    ProviderId,
    DateOnly UnavailableDate,
    string? Reason = null) : IRequest<Guid>;
```

Create `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommandValidator.cs`:

```csharp
using FluentValidation;

namespace HealthPlatform.Application.Features.Providers;

public sealed class CreateUnavailabilityCommandValidator
    : AbstractValidator<CreateUnavailabilityCommand>
{
    public CreateUnavailabilityCommandValidator()
    {
        RuleFor(x => x.ProviderId).NotEmpty();
        RuleFor(x => x.UnavailableDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("UnavailableDate cannot be in the past.");
        RuleFor(x => x.Reason).MaximumLength(500).When(x => x.Reason is not null);
    }
}
```

Create `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommandHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class CreateUnavailabilityCommandHandler
    : IRequestHandler<CreateUnavailabilityCommand, Guid>
{
    private readonly IUnitOfWork _uow;

    public CreateUnavailabilityCommandHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<Guid> Handle(CreateUnavailabilityCommand request, CancellationToken ct)
    {
        var entry = new ProviderUnavailability
        {
            Id              = Guid.NewGuid(),
            ProviderId      = request.ProviderId,
            UnavailableDate = request.UnavailableDate,
            Reason          = request.Reason
        };

        await _uow.Repository<ProviderUnavailability>().AddAsync(entry, ct);
        await _uow.SaveChangesAsync(ct);
        return entry.Id;
    }
}
```

---

### 4. `GetProviderSlotsQuery` + Handler + DTO

Create `src/HealthPlatform.Application/Features/Providers/GetProviderSlotsQuery.cs`:

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

public sealed record GetProviderSlotsQuery(Guid ProviderId, DateOnly Date)
    : IRequest<IReadOnlyList<SlotDto>>;

public sealed record SlotDto(
    Guid           SlotId,
    Guid           ProviderId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string         Status);
```

Create `src/HealthPlatform.Application/Features/Providers/GetProviderSlotsQueryHandler.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Providers;

internal sealed class GetProviderSlotsQueryHandler
    : IRequestHandler<GetProviderSlotsQuery, IReadOnlyList<SlotDto>>
{
    private readonly IUnitOfWork _uow;

    public GetProviderSlotsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<SlotDto>> Handle(
        GetProviderSlotsQuery query,
        CancellationToken     ct)
    {
        var from = new DateTimeOffset(
            query.Date.Year, query.Date.Month, query.Date.Day,
            0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var slots = await _uow.Repository<AppointmentSlot>()
            .GetAsync(
                new SlotsByProviderAndDateSpecification(
                    query.ProviderId, from, to), ct);

        return slots
            .Select(s => new SlotDto(
                s.Id, s.ProviderId, s.StartTime, s.EndTime, s.Status.ToString()))
            .ToList();
    }
}
```

---

### 5. Specifications

Create `src/HealthPlatform.Infrastructure/Persistence/Specifications/ScheduleRuleByProviderAndDaySpecification.cs`:

```csharp
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

public sealed class ScheduleRuleByProviderAndDaySpecification
    : BaseSpecification<ProviderScheduleRule>
{
    public ScheduleRuleByProviderAndDaySpecification(Guid providerId, DayOfWeek dayOfWeek)
        : base(r => r.ProviderId == providerId && r.DayOfWeek == dayOfWeek)
    { }
}
```

Create `src/HealthPlatform.Infrastructure/Persistence/Specifications/SlotsByProviderAndDateSpecification.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

/// <summary>
/// Returns all Available slots for a provider within a UTC date window,
/// ordered by start time. Booked and Blocked slots are excluded.
/// </summary>
public sealed class SlotsByProviderAndDateSpecification
    : BaseSpecification<AppointmentSlot>
{
    public SlotsByProviderAndDateSpecification(
        Guid           providerId,
        DateTimeOffset from,
        DateTimeOffset to)
        : base(s => s.ProviderId == providerId
                 && s.Status     == SlotStatus.Available
                 && s.StartTime  >= from
                 && s.StartTime  <  to)
    {
        ApplyOrderBy(s => s.StartTime);
    }
}
```

---

### 6. Update `AvailableSlotsByProviderAndDateRangeSpecification`

Edit the existing specification to use `Status` instead of `IsAvailable`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

public sealed class AvailableSlotsByProviderAndDateRangeSpecification
    : BaseSpecification<AppointmentSlot>
{
    public AvailableSlotsByProviderAndDateRangeSpecification(
        Guid           providerId,
        DateTimeOffset from,
        DateTimeOffset to)
        : base(s => s.ProviderId == providerId
                 && s.Status     == SlotStatus.Available
                 && s.StartTime  >= from
                 && s.StartTime  <  to)
    {
        ApplyOrderBy(s => s.StartTime);
    }
}
```

---

### 7. Replace `AppointmentSlotSeedService` with `SlotGenerationService`

Replace `src/HealthPlatform.Infrastructure/Persistence/Seed/AppointmentSlotSeedService.cs`
with `SlotGenerationService.cs` at the same path (rename file):

```csharp
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
/// exist or are already Booked (AC: schedule changes do not affect booked slots).
/// </summary>
internal sealed class SlotGenerationService : BackgroundService
{
    private readonly IServiceScopeFactory             _scopeFactory;
    private readonly ILogger<SlotGenerationService>   _logger;

    private static readonly TimeOnly DefaultStart    = new(9,  0);
    private static readonly TimeOnly DefaultEnd      = new(17, 0);
    private const           int      DefaultDuration = 30;
    private const           int      HorizonDays     = 90;

    public SlotGenerationService(IServiceScopeFactory scopeFactory,
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
            _logger.LogWarning("No providers found. Slot generation skipped.");
            return;
        }

        // Load all schedule rules (keyed by providerId)
        var rulesByProvider = await db.ProviderScheduleRules
            .Where(r => !r.IsDeleted)
            .ToListAsync(stoppingToken);

        var ruleMap = rulesByProvider
            .GroupBy(r => r.ProviderId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load unavailabilities within horizon
        var unavailabilities = await db.ProviderUnavailabilities
            .Where(u => !u.IsDeleted
                     && u.UnavailableDate >= today
                     && u.UnavailableDate <= horizon)
            .Select(u => new { u.ProviderId, u.UnavailableDate })
            .ToListAsync(stoppingToken);

        var unavailableSet = unavailabilities
            .ToHashSet(EqualityComparer<(Guid, DateOnly)>.Create(
                (a, b) => a.ProviderId == b.ProviderId
                       && a.UnavailableDate == b.UnavailableDate,
                x => HashCode.Combine(x.ProviderId, x.UnavailableDate)));

        // Load existing slot keys to avoid duplicates
        var existingKeys = await db.AppointmentSlots
            .Where(s => s.StartTime >= new DateTimeOffset(
                            today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero))
            .Select(s => new { s.ProviderId, s.StartTime })
            .ToListAsync(stoppingToken);

        var existingSet = existingKeys
            .Select(x => (x.ProviderId, x.StartTime))
            .ToHashSet();

        var newSlots = new List<AppointmentSlot>();
        var daysAdded = 0;

        for (var dayOffset = 0; dayOffset < HorizonDays; dayOffset++)
        {
            var date = today.AddDays(dayOffset);

            foreach (var providerId in providerIds)
            {
                var unavailableKey = (ProviderId: providerId, UnavailableDate: date);
                if (unavailableSet.Any(u => u.ProviderId == providerId
                                         && u.UnavailableDate == date))
                    continue;

                var dayRules = ruleMap.TryGetValue(providerId, out var rules)
                    ? rules.Where(r => r.DayOfWeek == date.DayOfWeek).ToList()
                    : [];

                TimeOnly start, end;
                int      duration;

                if (dayRules.Count > 0)
                {
                    var rule = dayRules[0];
                    start    = rule.StartTime;
                    end      = rule.EndTime;
                    duration = rule.SlotDurationMinutes;
                }
                else
                {
                    // Fall back to default 9–17 window
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
                "SlotGenerationService: all slots already up to date for the next {Days} days.",
                HorizonDays);
        }
    }
}
```

---

### 8. Update `DependencyInjection.cs` — Replace Registration

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`, replace
`AddHostedService<AppointmentSlotSeedService>()` with:

```csharp
services.AddHostedService<SlotGenerationService>();
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommand.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommandValidator.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/CreateScheduleRuleCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/DeleteScheduleRuleCommand.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/DeleteScheduleRuleCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommand.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommandValidator.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/CreateUnavailabilityCommandHandler.cs` | New |
| `src/HealthPlatform.Application/Features/Providers/GetProviderSlotsQuery.cs` | New (includes `SlotDto`) |
| `src/HealthPlatform.Application/Features/Providers/GetProviderSlotsQueryHandler.cs` | New |
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/ScheduleRuleByProviderAndDaySpecification.cs` | New |
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/SlotsByProviderAndDateSpecification.cs` | New |
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/AvailableSlotsByProviderAndDateRangeSpecification.cs` | Update: `IsAvailable` → `Status` |
| `src/HealthPlatform.Infrastructure/Persistence/Seed/AppointmentSlotSeedService.cs` | Replace → `SlotGenerationService` |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Update hosted service registration |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

All projects compile. Tests pass.
