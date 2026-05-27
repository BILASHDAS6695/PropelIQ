using HealthPlatform.Api.Authorization;
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
    /// <param name="date">Calendar date in <c>yyyy-MM-dd</c> format (e.g., <c>2026-06-15</c>).</param>
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
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(ScheduleRuleResponse),     StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),            StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateScheduleRule(
        Guid                        id,
        [FromBody] ScheduleRuleRequest request,
        CancellationToken           ct)
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
    [Authorize(Policy = PolicyNames.Admin)]
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
    [Authorize(Policy = PolicyNames.Admin)]
    [ProducesResponseType(typeof(UnavailabilityResponse),   StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateUnavailability(
        Guid                             id,
        [FromBody] UnavailabilityRequest request,
        CancellationToken                ct)
    {
        var entryId = await _sender.Send(
            new CreateUnavailabilityCommand(id, request.UnavailableDate, request.Reason), ct);

        return CreatedAtAction(
            nameof(GetSlots),
            new { id },
            new UnavailabilityResponse(entryId));
    }
}

// ── Request models ───────────────────────────────────────────────────────────

/// <summary>Payload for creating a recurring schedule rule.</summary>
public sealed record ScheduleRuleRequest(
    DayOfWeek DayOfWeek,
    TimeOnly  StartTime,
    TimeOnly  EndTime,
    int       SlotDurationMinutes = 30);

/// <summary>Payload for marking a calendar date unavailable.</summary>
public sealed record UnavailabilityRequest(
    DateOnly UnavailableDate,
    string?  Reason = null);

// ── Response models ──────────────────────────────────────────────────────────

public sealed record ScheduleRuleResponse(Guid RuleId);
public sealed record UnavailabilityResponse(Guid UnavailabilityId);
