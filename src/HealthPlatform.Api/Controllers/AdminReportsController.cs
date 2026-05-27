using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Appointments;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Admin-only analytics report endpoints.
/// </summary>
[ApiController]
[Route("api/admin/reports")]
[Authorize(Policy = PolicyNames.Admin)]
public sealed class AdminReportsController : ControllerBase
{
    private readonly ISender _sender;

    public AdminReportsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns the no-show analytics report for a given date range.
    /// Aggregates by provider, day of week, and time slot (UTC hour bucket).
    /// </summary>
    /// <param name="dateFrom">Start date inclusive (YYYY-MM-DD).</param>
    /// <param name="dateTo">End date inclusive (YYYY-MM-DD).</param>
    /// <param name="providerId">Optional provider filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — report data.<br/>
    /// 401 Unauthorized — caller is not authenticated.<br/>
    /// 403 Forbidden — caller does not have Admin role.<br/>
    /// 422 Unprocessable Entity — date range exceeds 90 days or DateTo &lt; DateFrom.
    /// </returns>
    [HttpGet("no-shows")]
    [ProducesResponseType(typeof(NoShowReportDto),          StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetNoShowReport(
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        [FromQuery] Guid?    providerId,
        CancellationToken    ct)
    {
        var report = await _sender.Send(
            new GetNoShowReportQuery(dateFrom, dateTo, providerId), ct);

        return Ok(report);
    }
}
