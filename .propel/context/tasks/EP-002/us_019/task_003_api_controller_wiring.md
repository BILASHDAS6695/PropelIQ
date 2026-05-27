# Task 003: API Controller & End-to-End Wiring

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-019 |
| **Epic** | EP-002 |
| **Layer** | API (controller + request/response models) |
| **Priority** | Critical |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (domain model), Task 002 (CQRS handlers registered) |

## Objective

Expose provider schedule management and slot retrieval via a REST controller.
Implement the required `GET /providers/{id}/slots?date={date}` endpoint plus
admin endpoints for managing schedule rules and unavailability blocks. Validate
inputs, map HTTP layer to MediatR commands/queries, and return RFC 7807
problem details on failure.

## Acceptance Criteria Covered

- AC: `GET /providers/{id}/slots?date={date}` returns available slots
- AC: Admin can define recurring weekly schedule per provider (POST endpoint)
- AC: Admin can mark specific dates as unavailable (POST endpoint)
- AC: Provider with no schedule → returns empty list (handled by handler)

---

## Implementation Steps

### 1. Create `ProvidersController`

Create `src/HealthPlatform.Api/Controllers/ProvidersController.cs`:

```csharp
using HealthPlatform.Application.Features.Providers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Provider schedule management and slot availability endpoints.
/// </summary>
[ApiController]
[Route("api/providers")]
public sealed class ProvidersController : ControllerBase
{
    private readonly ISender _sender;

    public ProvidersController(ISender sender) => _sender = sender;

    // ─── Slots ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all available appointment slots for the specified provider on
    /// the given date (UTC calendar day).
    /// </summary>
    /// <param name="id">Provider ID.</param>
    /// <param name="date">
    /// Calendar date in <c>yyyy-MM-dd</c> format (e.g., <c>2026-06-15</c>).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — list of available slots (empty array when none).<br/>
    /// 400 Bad Request — <c>date</c> parameter is missing or invalid.
    /// </returns>
    [HttpGet("{id:guid}/slots")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails),         StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSlots(
        Guid              id,
        [FromQuery] string date,
        CancellationToken ct)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsedDate))
            return BadRequest(new ProblemDetails
            {
                Title  = "Invalid date format.",
                Detail = "The 'date' query parameter must be in yyyy-MM-dd format.",
                Status = StatusCodes.Status400BadRequest
            });

        var slots = await _sender.Send(new GetProviderSlotsQuery(id, parsedDate), ct);
        return Ok(slots);
    }

    // ─── Schedule Rules ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a recurring weekly schedule rule for the specified provider.
    /// Returns 409 Conflict if a rule already exists for the same day of week.
    /// </summary>
    /// <param name="id">Provider ID.</param>
    /// <param name="request">Schedule rule payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <c>{ ruleId }</c>.<br/>
    /// 409 Conflict — rule already exists for the given day of week.<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost("{id:guid}/schedule-rules")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(ScheduleRuleResponse),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),         StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateScheduleRule(
        Guid                       id,
        [FromBody] ScheduleRuleRequest request,
        CancellationToken          ct)
    {
        Guid ruleId;
        try
        {
            ruleId = await _sender.Send(
                new CreateScheduleRuleCommand(
                    id,
                    request.DayOfWeek,
                    request.StartTime,
                    request.EndTime,
                    request.SlotDurationMinutes), ct);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ProblemDetails
            {
                Title  = "Duplicate schedule rule.",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }

        return CreatedAtAction(
            nameof(GetSlots),
            new { id },
            new ScheduleRuleResponse(ruleId));
    }

    /// <summary>
    /// Deletes a schedule rule by its ID.
    /// </summary>
    /// <param name="id">Provider ID (for route consistency).</param>
    /// <param name="ruleId">Schedule rule ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 No Content — deleted.<br/>
    /// 404 Not Found — rule does not exist.
    /// </returns>
    [HttpDelete("{id:guid}/schedule-rules/{ruleId:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteScheduleRule(
        Guid              id,
        Guid              ruleId,
        CancellationToken ct)
    {
        try
        {
            await _sender.Send(new DeleteScheduleRuleCommand(ruleId), ct);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Schedule rule not found.",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }

        return NoContent();
    }

    // ─── Unavailabilities ────────────────────────────────────────────────────

    /// <summary>
    /// Marks a specific calendar date as unavailable for the provider
    /// (vacation, public holiday, etc.).
    /// </summary>
    /// <param name="id">Provider ID.</param>
    /// <param name="request">Unavailability payload.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <c>{ unavailabilityId }</c>.<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost("{id:guid}/unavailabilities")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UnavailabilityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateUnavailability(
        Guid                            id,
        [FromBody] UnavailabilityRequest request,
        CancellationToken               ct)
    {
        var entryId = await _sender.Send(
            new CreateUnavailabilityCommand(id, request.UnavailableDate, request.Reason), ct);

        return CreatedAtAction(
            nameof(GetSlots),
            new { id },
            new UnavailabilityResponse(entryId));
    }
}
```

---

### 2. Create Request / Response Models

Create `src/HealthPlatform.Api/Controllers/ProvidersController.cs` companion
models (or a dedicated `Models/` file under the same namespace):

```csharp
// ── Request models ──────────────────────────────────────────────────────────

/// <summary>Payload for creating a recurring schedule rule.</summary>
public sealed record ScheduleRuleRequest(
    DayOfWeek DayOfWeek,
    TimeOnly  StartTime,
    TimeOnly  EndTime,
    int       SlotDurationMinutes = 30);

/// <summary>Payload for marking a date unavailable.</summary>
public sealed record UnavailabilityRequest(
    DateOnly UnavailableDate,
    string?  Reason = null);

// ── Response models ──────────────────────────────────────────────────────────

public sealed record ScheduleRuleResponse(Guid RuleId);
public sealed record UnavailabilityResponse(Guid UnavailabilityId);
```

> **Placement**: Add these records at the bottom of `ProvidersController.cs`
> (outside the class, inside the namespace) to keep the file self-contained.
> Move to separate files if the project adopts a Models/ convention.

---

### 3. Verify Swagger / OpenAPI Produces Correct Schema

No changes to `Program.cs` are required — `AddControllers()` already scans all
assemblies. Swagger will pick up `ProvidersController` automatically.

Confirm by running the application and navigating to `/swagger` — verify that:
- `GET /api/providers/{id}/slots` is listed with `date` query param
- `POST /api/providers/{id}/schedule-rules` is listed under Admin
- `POST /api/providers/{id}/unavailabilities` is listed under Admin

---

### 4. Update Unit / Integration Tests (Smoke)

Add basic tests in `src/HealthPlatform.Tests/Application/`:

**`GetProviderSlotsQueryTests.cs`** (minimal):
- Given a provider with no slots → handler returns empty list
- Given a provider with Available slots on the queried date → handler returns those slots

These tests use in-memory `IUnitOfWork` mock or the existing test infrastructure
pattern (see `PingQueryTests.cs` for structure).

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/ProvidersController.cs` | New — 4 endpoints + request/response records |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

Manual smoke test sequence:
```
# 1. Login as Admin → capture JWT
POST /api/auth/login  { email, password }

# 2. Create schedule rule for Monday
POST /api/providers/{id}/schedule-rules
{ "dayOfWeek": 1, "startTime": "09:00:00", "endTime": "17:00:00", "slotDurationMinutes": 30 }
→ 201 Created { ruleId }

# 3. Attempt duplicate → 409 Conflict
POST /api/providers/{id}/schedule-rules (same dayOfWeek)

# 4. Mark a date unavailable
POST /api/providers/{id}/unavailabilities
{ "unavailableDate": "2026-06-15", "reason": "Public holiday" }
→ 201 Created { unavailabilityId }

# 5. Get slots for that date → empty (unavailability blocks slot generation)
GET /api/providers/{id}/slots?date=2026-06-15
→ 200 []

# 6. Get slots for a date with rules
GET /api/providers/{id}/slots?date=2026-06-22
→ 200 [ { slotId, startTime, endTime, status: "Available" }, ... ]

# 7. Invalid date format
GET /api/providers/{id}/slots?date=22-06-2026
→ 400 Bad Request
```

## Notes

- `[Authorize(Roles = "Admin")]` uses the `UserRole` claim already set during
  JWT generation in `LoginCommandHandler`. No additional middleware needed.
- The `GetSlots` action uses `[Authorize]` (not Admin-only) so patients and
  clinicians can also query availability.
- `DateOnly` and `TimeOnly` serialize/deserialize correctly in .NET 8 System.Text.Json
  without extra converters. Confirm the swagger schema renders them as `string`
  with `date` / `time` format.
- The `SlotGenerationService` runs once on startup. To refresh the 90-day
  window daily, a follow-on story should use `IHostedService` with a
  `PeriodicTimer` or a scheduled job (Hangfire / Quartz).
