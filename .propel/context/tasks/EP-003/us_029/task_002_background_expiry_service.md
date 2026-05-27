# Task 002: Background Service — Swap Request Auto-Expiry

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-029 |
| **Epic** | EP-003 |
| **Layer** | Infrastructure (Background Service) |
| **Priority** | Medium |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | US-028 Task 001 (`SlotSwapRequest` entity, `SlotSwapStatus.Expired`), Task 001 of this story (`IEmailSender` is used for requester notification on expiry) |

## Objective

Implement a recurring background service that sweeps `SlotSwapRequest` rows where
`Status = Pending` and `ExpiresAt < UtcNow`, transitions them to `Expired`, and
sends a notification email to the requester so they know their offer lapsed.

This closes the edge-case AC:
> Target patient doesn't respond within 24h → auto-decline (stored as `Expired`)

The sweep runs every **5 minutes** so expired requests are cleaned up promptly
without hammering the DB.

## Acceptance Criteria Covered

- AC: Swap request expires after 24 hours if no response (auto-expire to `Expired`)
- AC: Both patients' calendar views update immediately after swap (expiry surfaces
  as an `Expired` status visible via any GET endpoint)
- Edge case: Target patient doesn't respond within 24h → auto-decline

---

## Implementation Steps

### 1. New Specification — `ExpiredPendingSwapRequestsSpecification`

Create `src/HealthPlatform.Application/Features/SlotSwap/ExpiredPendingSwapRequestsSpecification.cs`:

```csharp
using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Application.Features.SlotSwap;

/// <summary>
/// Matches <see cref="SlotSwapRequest"/> rows that are still <c>Pending</c>
/// but whose expiry timestamp has passed. Used by the auto-expiry background sweep.
/// </summary>
internal sealed class ExpiredPendingSwapRequestsSpecification : ISpecification<SlotSwapRequest>
{
    private readonly DateTimeOffset _now;

    public ExpiredPendingSwapRequestsSpecification(DateTimeOffset now) => _now = now;

    public Expression<Func<SlotSwapRequest, bool>>? Criteria =>
        r => r.Status == SlotSwapStatus.Pending && r.ExpiresAt <= _now;

    public List<Expression<Func<SlotSwapRequest, object>>> Includes { get; } =
    [
        r => r.RequesterPatient,
    ];

    public Expression<Func<SlotSwapRequest, object>>?      OrderBy           => null;
    public Expression<Func<SlotSwapRequest, object>>?      OrderByDescending => null;
    public bool                                            IsPagingEnabled   => false;
    public int                                             Skip              => 0;
    public int                                             Take              => 0;
}
```

> The `RequesterPatient` include gives us `RequesterPatient.UserId` so we can load
> the `User.Email` in a follow-up `GetByIdAsync` without an extra spec round-trip.

---

### 2. New Background Service — `SwapRequestExpiryService`

Create `src/HealthPlatform.Infrastructure/Messaging/SwapRequestExpiryService.cs`:

```csharp
using HealthPlatform.Application.Features.SlotSwap;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Periodic background sweep that expires <see cref="SlotSwapRequest"/> rows
/// whose <c>ExpiresAt</c> has passed while still in <c>Pending</c> status.
/// Runs every <see cref="SweepInterval"/> minutes.
/// </summary>
internal sealed class SwapRequestExpiryService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly ILogger<SwapRequestExpiryService> _logger;

    public SwapRequestExpiryService(
        IServiceScopeFactory              scopeFactory,
        ILogger<SwapRequestExpiryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SwapRequestExpiryService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "SwapRequestExpiryService sweep failed.");
            }

            await Task.Delay(SweepInterval, stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var uow   = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        var now  = DateTimeOffset.UtcNow;
        var spec = new ExpiredPendingSwapRequestsSpecification(now);

        var expired = await uow.Repository<SlotSwapRequest>().GetAsync(spec, ct);

        if (expired.Count == 0)
            return;

        _logger.LogInformation(
            "SwapRequestExpiryService: expiring {Count} swap request(s).", expired.Count);

        var swapRepo = uow.Repository<SlotSwapRequest>();

        foreach (var request in expired)
        {
            request.Status = SlotSwapStatus.Expired;
            swapRepo.Update(request);

            // Notify requester their offer expired
            var requesterUser = await uow.Repository<User>()
                .GetByIdAsync(request.RequesterPatient.UserId, ct);

            if (requesterUser is not null)
            {
                await email.SendAsync(
                    requesterUser.Email,
                    "Your slot swap request has expired",
                    "Your slot swap offer was not responded to within 24 hours and has expired. " +
                    "You may submit a new swap request if you still wish to change your appointment time.",
                    ct);
            }
        }

        await uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "SwapRequestExpiryService: {Count} swap request(s) marked Expired.", expired.Count);
    }
}
```

---

### 3. Register in Infrastructure DI

Edit `src/HealthPlatform.Infrastructure/DependencyInjection.cs`.

Add the hosted service registration immediately after the existing
`AddHostedService<SlotGenerationService>()` line:

```csharp
services.AddHostedService<SwapRequestExpiryService>();
```

Add the using at the top of the file if not already present:

```csharp
using HealthPlatform.Infrastructure.Messaging;
```

> `SwapRequestExpiryService` is in the `HealthPlatform.Infrastructure.Messaging`
> namespace (same file as `NoOpEmailSender`). The using may already be present.

---

## Files Modified / Created

| Action | Path |
|--------|------|
| CREATE | `src/HealthPlatform.Application/Features/SlotSwap/ExpiredPendingSwapRequestsSpecification.cs` |
| CREATE | `src/HealthPlatform.Infrastructure/Messaging/SwapRequestExpiryService.cs` |
| EDIT   | `src/HealthPlatform.Infrastructure/DependencyInjection.cs` |

## Verification

- `dotnet build src/HealthPlatform.sln` → 0 errors
- On API startup, log line: `"SwapRequestExpiryService started."` appears
- Seed a `SlotSwapRequest` with `ExpiresAt = UtcNow - 1 minute`, wait 5 minutes
  → Status becomes `Expired` in the DB and `NoOpEmailSender` logs a "has expired" email
- If DB is unreachable during sweep → error is logged but service continues running
  (the `catch` block prevents crash)
