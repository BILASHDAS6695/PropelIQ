# Task 003: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-020 |
| **Epic** | EP-002 |
| **Layer** | API (controllers + request/response models) |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 + Task 002 (domain + CQRS handlers registered) |

## Objective

Expose the booking flow and provider listing via REST endpoints. Add
`GET /api/providers` to `ProvidersController` and create a new
`AppointmentsController` with `POST /api/appointments`. Map HTTP inputs to
MediatR commands/queries, return RFC 7807 problem details on all failure paths,
and add a smoke test for the booking endpoint using an in-memory stub.

## Acceptance Criteria Covered

- AC: Patient selects provider from list (filtered by specialty optional)
- AC: Booking confirmation response: provider name, date, time, appointment ID
- AC: Two patients book same slot → first wins, second gets "Slot no longer available"
  (HTTP 409)

---

## Implementation Steps

### 1. Add `GET /api/providers` to `ProvidersController`

Edit `src/HealthPlatform.Api/Controllers/ProvidersController.cs` — add the new
action before the existing schedule-rule section:

```csharp
// ─── Provider Listing ─────────────────────────────────────────────────────

/// <summary>
/// Returns all active providers, optionally filtered by specialty
/// (case-insensitive substring match), ordered by name.
/// </summary>
/// <param name="specialty">
/// Optional specialty filter (e.g., <c>Cardiology</c>).
/// Omit to return all providers.
/// </param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — list of providers (empty array when none match).
/// </returns>
[HttpGet]
[Authorize]
[ProducesResponseType(typeof(IReadOnlyList<ProviderDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetProviders(
    [FromQuery] string? specialty,
    CancellationToken   ct)
{
    var providers = await _sender.Send(new GetProvidersQuery(specialty), ct);
    return Ok(providers);
}
```

---

### 2. Create `AppointmentsController`

Create `src/HealthPlatform.Api/Controllers/AppointmentsController.cs`:

```csharp
using HealthPlatform.Application.Features.Appointments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient appointment booking endpoints.
/// </summary>
[ApiController]
[Route("api/appointments")]
public sealed class AppointmentsController : ControllerBase
{
    private readonly ISender _sender;

    public AppointmentsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Books an available appointment slot for the authenticated patient.
    /// </summary>
    /// <param name="request">Booking payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — booking confirmation with provider name, date, time,
    ///   and appointment ID.<br/>
    /// 400 Bad Request — slot ID missing or visit reason exceeds 500 chars.<br/>
    /// 409 Conflict — slot no longer available (taken by another patient or
    ///   duplicate active appointment on same provider/day).<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(BookingConfirmationDto),   StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Book(
        [FromBody] BookAppointmentRequest request,
        CancellationToken                 ct)
    {
        BookingConfirmationDto confirmation;
        try
        {
            confirmation = await _sender.Send(
                new BookAppointmentCommand(request.SlotId, request.VisitReason), ct);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Booking failed.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new ProblemDetails
            {
                Title  = "Unauthorized.",
                Detail = ex.Message,
                Status = StatusCodes.Status401Unauthorized
            });
        }

        return CreatedAtAction(
            nameof(Book),
            new { appointmentId = confirmation.AppointmentId },
            confirmation);
    }
}

// ── Request model ─────────────────────────────────────────────────────────────

/// <summary>Payload for booking an appointment slot.</summary>
public sealed record BookAppointmentRequest(
    Guid    SlotId,
    string? VisitReason = null);
```

---

### 3. Add Smoke Test

Create `src/HealthPlatform.Tests/Application/BookAppointmentCommandTests.cs`:

```csharp
using HealthPlatform.Application;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class BookAppointmentCommandTests
{
    [Fact]
    public async Task Book_UnavailableSlot_ThrowsInvalidOperationException()
    {
        // Arrange — slot already Booked
        var providerId = Guid.NewGuid();
        var slot = new AppointmentSlot
        {
            Id         = Guid.NewGuid(),
            ProviderId = providerId,
            StartTime  = DateTimeOffset.UtcNow.AddDays(1),
            EndTime    = DateTimeOffset.UtcNow.AddDays(1).AddMinutes(30),
            Status     = SlotStatus.Booked   // not available
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IUnitOfWork>(_ => new BookingStubUnitOfWork(slot));
        services.AddScoped<ICurrentUserService>(_ => new AuthenticatedStubUser(Guid.NewGuid()));
        services.AddScoped<IEmailSender>(_ => new NoOpStubEmailSender());
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.Send(new BookAppointmentCommand(slot.Id)));
    }

    [Fact]
    public async Task Book_UnauthenticatedUser_ThrowsUnauthorizedAccessException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IUnitOfWork>(_ => new BookingStubUnitOfWork(null));
        services.AddScoped<ICurrentUserService>(_ => new AnonymousStubUser());
        services.AddScoped<IEmailSender>(_ => new NoOpStubEmailSender());
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => sender.Send(new BookAppointmentCommand(Guid.NewGuid())));
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────

internal sealed class BookingStubUnitOfWork : IUnitOfWork
{
    private readonly AppointmentSlot? _slot;

    public BookingStubUnitOfWork(AppointmentSlot? slot) => _slot = slot;

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(AppointmentSlot))
            return (IRepository<T>)(object)new SingleSlotRepository(_slot);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    public void Dispose() { }
}

internal sealed class SingleSlotRepository : IRepository<AppointmentSlot>
{
    private readonly AppointmentSlot? _slot;

    public SingleSlotRepository(AppointmentSlot? slot) => _slot = slot;

    public Task<AppointmentSlot?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_slot?.Id == id ? _slot : null);

    public Task<IReadOnlyList<AppointmentSlot>> GetAsync(
        ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppointmentSlot>>(Array.Empty<AppointmentSlot>());

    public Task<IReadOnlyList<AppointmentSlot>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AppointmentSlot>>(Array.Empty<AppointmentSlot>());

    public Task<int> CountAsync(ISpecification<AppointmentSlot> spec, CancellationToken ct = default)
        => Task.FromResult(0);

    public Task AddAsync(AppointmentSlot entity, CancellationToken ct = default)
        => Task.CompletedTask;

    public void Update(AppointmentSlot entity) { }
    public void Delete(AppointmentSlot entity) { }
}

internal sealed class AuthenticatedStubUser : ICurrentUserService
{
    public AuthenticatedStubUser(Guid userId) => UserId = userId;
    public Guid? UserId          { get; }
    public bool  IsAuthenticated => true;
}

internal sealed class AnonymousStubUser : ICurrentUserService
{
    public Guid? UserId          => null;
    public bool  IsAuthenticated => false;
}

internal sealed class NoOpStubEmailSender : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken ct = default)
        => Task.CompletedTask;
}
```

> **Note**: `EmptyRepository<T>` is already defined in `GetProviderSlotsQueryTests.cs`.
> Extract it to a shared `TestHelpers.cs` file in the Tests project to avoid
> duplication if it grows; for now define it locally as a private sealed class
> in the same test file.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/ProvidersController.cs` | Add `GET /api/providers` action |
| `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` | New — `POST /api/appointments` + `BookAppointmentRequest` |
| `src/HealthPlatform.Tests/Application/BookAppointmentCommandTests.cs` | New — 2 smoke tests |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

All tests pass (6 existing + 2 new = 8 total).

## Manual Smoke Test Sequence

```
# 1. Login as patient → capture JWT
POST /api/auth/login  { "email": "patient@example.com", "password": "..." }

# 2. List providers (no filter)
GET /api/providers
→ 200 [ { providerId, name, specialty }, ... ]

# 3. List providers filtered by specialty
GET /api/providers?specialty=Cardiology
→ 200 [ { ..., specialty: "Cardiology" } ]

# 4. View available slots for a provider on a date
GET /api/providers/{id}/slots?date=2026-06-15
→ 200 [ { slotId, startTime, endTime, status: "Available" }, ... ]

# 5. Book a slot
POST /api/appointments
{ "slotId": "{slotId}", "visitReason": "Annual check-up" }
→ 201 { appointmentId, providerId, providerName, appointmentTime, status: "Scheduled" }

# 6. Book same slot again (concurrent race)
POST /api/appointments  { "slotId": "{same slotId}" }
→ 409 { title: "Booking failed.", detail: "This slot is no longer available..." }

# 7. Book another slot with same provider on same day (duplicate guard)
POST /api/appointments  { "slotId": "{another slotId same provider same day}" }
→ 409 { detail: "You already have an active appointment with this provider on the requested date." }

# 8. Invalid visit reason (> 500 chars)
POST /api/appointments  { "slotId": "...", "visitReason": "x".repeat(501) }
→ 422 Unprocessable Entity (FluentValidation)
```

## Notes

- The `AppointmentsController` catches `InvalidOperationException` generically to cover
  both the "slot not available" and "duplicate appointment" cases — both are
  surfaced as 409 Conflict with a descriptive `Detail` field.
- `KeyNotFoundException` (slot or provider not found) propagates to the global
  exception handler which returns 404 — no explicit catch needed.
- `UnauthorizedAccessException` is caught explicitly because the global handler
  may not convert it to 401 in all middleware configurations.
- For production, consider extracting a domain `SlotUnavailableException` to
  distinguish between concurrency failures and business-rule violations at the
  HTTP mapping layer.
