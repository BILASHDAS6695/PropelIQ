# Task 003: API Controllers & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-021 |
| **Epic** | EP-002 |
| **Layer** | API (controllers + request models) |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 + Task 002 (domain + CQRS handlers registered) |

## Objective

Expose walk-in registration, patient quick-create, and the provider daily queue
via REST endpoints. Three surfaces: new `PatientsController`, a new action on
`AppointmentsController`, and a new action on `ProvidersController`.

## Acceptance Criteria Covered

- AC: Staff can register walk-in (POST /api/appointments/walk-in)
- AC: Staff can quick-create patient (POST /api/patients/quick-create)
- AC: Walk-in visible in provider's daily queue (GET /api/providers/{id}/queue)

---

## Implementation Steps

### 1. New `PatientsController`

Create `src/HealthPlatform.Api/Controllers/PatientsController.cs`:

```csharp
using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient management endpoints for front-desk staff.
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender _sender;

    public PatientsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Quick-creates a patient profile for an unregistered walk-in.
    /// Creates a placeholder User (IsActive = false) and a PatientProfile.
    /// The patient cannot log in via the portal; this is a staff-only operation.
    /// </summary>
    /// <param name="request">Minimal patient details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <c>{ patientProfileId, userId }</c>.<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost("quick-create")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(QuickCreatePatientResult),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),   StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuickCreate(
        [FromBody] QuickCreatePatientRequest request,
        CancellationToken                    ct)
    {
        var result = await _sender.Send(
            new QuickCreatePatientCommand(
                request.FirstName,
                request.LastName,
                request.Dob,
                request.Phone), ct);

        return CreatedAtAction(nameof(QuickCreate), new { id = result.PatientProfileId }, result);
    }
}

/// <summary>Payload for quick-creating a walk-in patient profile.</summary>
public sealed record QuickCreatePatientRequest(
    string   FirstName,
    string   LastName,
    DateOnly Dob,
    string?  Phone = null);
```

---

### 2. Add Walk-In Action to `AppointmentsController`

Edit `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` — add after the existing `Book` action:

```csharp
/// <summary>
/// Registers a walk-in appointment for an existing patient.
/// Does not consume a pre-booked slot; auto-assigns a queue position.
/// </summary>
/// <param name="request">Walk-in registration payload.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 201 Created — walk-in confirmation with queue position and arrival time.<br/>
/// 404 Not Found — patient or provider does not exist.<br/>
/// 422 Unprocessable Entity — validation failed.
/// </returns>
[HttpPost("walk-in")]
[Authorize(Policy = PolicyNames.Staff)]
[ProducesResponseType(typeof(WalkInConfirmationDto),    StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> RegisterWalkIn(
    [FromBody] RegisterWalkInRequest request,
    CancellationToken                ct)
{
    var confirmation = await _sender.Send(
        new RegisterWalkInCommand(request.PatientId, request.ProviderId, request.VisitReason), ct);

    return CreatedAtAction(
        nameof(RegisterWalkIn),
        new { appointmentId = confirmation.AppointmentId },
        confirmation);
}
```

Also add the request record at the bottom of the file (after `BookAppointmentRequest`):

```csharp
/// <summary>Payload for registering a walk-in appointment.</summary>
public sealed record RegisterWalkInRequest(
    Guid    PatientId,
    Guid    ProviderId,
    string? VisitReason = null);
```

Required `using` additions at the top of `AppointmentsController.cs`:

```csharp
using HealthPlatform.Api.Authorization;
```

---

### 3. Add Queue Action to `ProvidersController`

Edit `src/HealthPlatform.Api/Controllers/ProvidersController.cs` — add after the `GetSlots` action, before the `CreateScheduleRule` section:

```csharp
// ─── Provider Queue ───────────────────────────────────────────────────────

/// <summary>
/// Returns the provider's daily appointment queue for the given date —
/// both scheduled online bookings and walk-in patients, ordered by
/// SlotTime (scheduled) and arrival order (walk-ins).
/// </summary>
/// <param name="id">Provider ID.</param>
/// <param name="date">Calendar date in <c>yyyy-MM-dd</c> format.</param>
/// <param name="ct">Cancellation token.</param>
/// <returns>
/// 200 OK — combined queue list (empty array when none).<br/>
/// 400 Bad Request — <c>date</c> parameter is missing or invalid.
/// </returns>
[HttpGet("{id:guid}/queue")]
[Authorize]
[ProducesResponseType(typeof(IReadOnlyList<QueueEntryDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),               StatusCodes.Status400BadRequest)]
public async Task<IActionResult> GetQueue(
    Guid               id,
    [FromQuery] string date,
    CancellationToken  ct)
{
    if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
        return BadRequest(new ProblemDetails
        {
            Title  = "Invalid date format.",
            Detail = "The 'date' query parameter must be in yyyy-MM-dd format.",
            Status = StatusCodes.Status400BadRequest
        });

    var queue = await _sender.Send(new GetProviderQueueQuery(id, parsedDate), ct);
    return Ok(queue);
}
```

---

### 4. Tests

Create `src/HealthPlatform.Tests/Application/RegisterWalkInCommandTests.cs`:

```csharp
using HealthPlatform.Application;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class RegisterWalkInCommandTests
{
    private static ISender BuildSender(IUnitOfWork uow)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped(_ => uow);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    [Fact]
    public async Task RegisterWalkIn_ProviderNotFound_ThrowsNotFoundException()
    {
        // Arrange — no provider in repo
        var sender = BuildSender(new WalkInStubUnitOfWork(null, null));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sender.Send(new RegisterWalkInCommand(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task RegisterWalkIn_ValidPatientAndProvider_ReturnsQueuePosition1()
    {
        // Arrange — provider + patient exist, no existing walk-ins today
        var provider = new Provider { Id = Guid.NewGuid(), Name = "Dr. Smith" };
        var patient  = new PatientProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        var sender = BuildSender(new WalkInStubUnitOfWork(provider, patient));

        // Act
        var result = await sender.Send(
            new RegisterWalkInCommand(patient.Id, provider.Id, "Headache"));

        // Assert
        Assert.Equal(1,               result.QueuePosition);
        Assert.Equal(provider.Id,     result.ProviderId);
        Assert.Equal(provider.Name,   result.ProviderName);
        Assert.Equal("WalkIn",        result.Status);
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────

internal sealed class WalkInStubUnitOfWork : IUnitOfWork
{
    private readonly Provider?       _provider;
    private readonly PatientProfile? _patient;

    public WalkInStubUnitOfWork(Provider? provider, PatientProfile? patient)
    {
        _provider = provider;
        _patient  = patient;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(Provider))
            return (IRepository<T>)(object)new SingleEntityRepository<Provider>(_provider);

        if (typeof(T) == typeof(PatientProfile))
            return (IRepository<T>)(object)new SingleEntityRepository<PatientProfile>(_patient);

        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    public void Dispose() { }
}

/// <summary>
/// Generic single-entity stub — returns the given entity for GetByIdAsync
/// and wraps it in a list for GetAsync. Reusable across test files.
/// </summary>
internal sealed class SingleEntityRepository<T> : IRepository<T> where T : class
{
    private readonly T? _entity;

    public SingleEntityRepository(T? entity) => _entity = entity;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_entity);

    public Task<IReadOnlyList<T>> GetAsync(
        ISpecification<T> spec, CancellationToken ct = default)
    {
        IReadOnlyList<T> result = _entity is null
            ? Array.Empty<T>()
            : [_entity];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<T> result = _entity is null ? Array.Empty<T>() : [_entity];
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Task.FromResult(_entity is null ? 0 : 1);

    public Task AddAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(T entity) { }
    public void Delete(T entity) { }
}
```

> **Note**: `EmptyRepository<T>` is already defined in `GetProviderSlotsQueryTests.cs`
> and accessible throughout the `HealthPlatform.Tests` assembly (same namespace).
> `SingleEntityRepository<T>` defined here is also accessible assembly-wide.
> Consolidate both into a shared `TestHelpers.cs` if the file count grows.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/PatientsController.cs` | New — `POST /api/patients/quick-create` + `QuickCreatePatientRequest` |
| `src/HealthPlatform.Api/Controllers/AppointmentsController.cs` | Add `POST /api/appointments/walk-in` + `RegisterWalkInRequest` |
| `src/HealthPlatform.Api/Controllers/ProvidersController.cs` | Add `GET /api/providers/{id}/queue?date=` |
| `src/HealthPlatform.Tests/Application/RegisterWalkInCommandTests.cs` | New — 2 tests + `WalkInStubUnitOfWork` + `SingleEntityRepository<T>` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

All tests pass (8 existing + 2 new = 10 total).

## Manual Smoke Test Sequence

```
# Auth as staff
POST /api/auth/login { "email": "staff@example.com", "password": "..." }

# 1. Quick-create walk-in patient
POST /api/patients/quick-create
{ "firstName": "Jane", "lastName": "Doe", "dob": "1990-03-15", "phone": "555-1234" }
→ 201 { patientProfileId, userId }

# 2. Register walk-in appointment
POST /api/appointments/walk-in
{ "patientId": "{patientProfileId}", "providerId": "{providerId}", "visitReason": "Back pain" }
→ 201 { appointmentId, patientId, providerId, providerName, queuePosition: 1, arrivalTime, status: "WalkIn" }

# 3. Second walk-in same provider same day → queue position 2
POST /api/appointments/walk-in
{ "patientId": "{anotherPatientId}", "providerId": "{same providerId}" }
→ 201 { ..., queuePosition: 2 }

# 4. View provider queue (includes scheduled + walk-ins)
GET /api/providers/{id}/queue?date=2026-05-27
→ 200 [ { appointmentId, patientId, status, appointmentTime, queuePosition, isWalkIn }, ... ]

# 5. Register walk-in for unknown provider → 404
POST /api/appointments/walk-in
{ "patientId": "{id}", "providerId": "00000000-0000-0000-0000-000000000000" }
→ 404 Not Found

# 6. Quick-create with missing fields → 422
POST /api/patients/quick-create { "firstName": "Jane" }
→ 422 Unprocessable Entity
```

## Notes

- `PatientsController` uses `[Authorize(Policy = PolicyNames.Staff)]` — only
  front-desk staff may create placeholder patient accounts.
- `POST /api/appointments/walk-in` also uses `PolicyNames.Staff`.
- `GET /api/providers/{id}/queue` uses `[Authorize]` — visible to staff and
  authenticated providers alike.
- `QuickCreatePatientCommandHandler` sets `User.IsActive = false` so walk-in
  placeholder accounts cannot authenticate via the portal.
- Walk-in queue position is computed with `MAX + 1` pattern. Under concurrent
  registrations, `UnitOfWork.SaveChangesAsync` will surface
  `DbUpdateConcurrencyException` as `ConflictException` → HTTP 409 via the
  `xmin` row-version on `Appointment`. Staff should retry.
