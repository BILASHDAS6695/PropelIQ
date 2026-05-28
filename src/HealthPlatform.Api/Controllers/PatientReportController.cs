using HealthPlatform.Application.Features.PdfReport;
using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// On-demand PDF appointment summary report endpoints.
/// </summary>
[ApiController]
[Route("api/patients/{patientId:guid}/appointments")]
[Authorize]
public sealed class PatientReportController : ControllerBase
{
    private readonly ISender             _sender;
    private readonly ICurrentUserService _currentUser;

    public PatientReportController(ISender sender, ICurrentUserService currentUser)
    {
        _sender      = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Requests a PDF appointment summary report for a patient.
    /// Returns a download link immediately; if more than 50 appointments are
    /// found the PDF is generated asynchronously and <c>isReady</c> will be
    /// <c>false</c> until the Hangfire job completes.
    /// </summary>
    /// <param name="patientId">Patient profile ID.</param>
    /// <param name="from">Range start (ISO 8601). Defaults to 12 months ago when omitted.</param>
    /// <param name="to">Range end (ISO 8601). Defaults to now when omitted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — <see cref="AppointmentReportResponseDto"/> with download URL.<br/>
    /// 403 Forbidden — caller is not the patient and is not staff/admin.<br/>
    /// 404 Not Found — patient profile does not exist.
    /// </returns>
    [HttpGet("pdf")]
    [ProducesResponseType(typeof(AppointmentReportResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestReport(
        Guid                    patientId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken           ct)
    {
        var now      = DateTimeOffset.UtcNow;
        var dateFrom = from ?? now.AddMonths(-12);
        var dateTo   = to   ?? now;

        var result = await _sender.Send(
            new RequestAppointmentReportCommand(patientId, dateFrom, dateTo), ct);

        return Ok(result);
    }

    /// <summary>
    /// Downloads a previously requested PDF appointment summary.
    /// </summary>
    /// <param name="patientId">Patient profile ID.</param>
    /// <param name="token">Unique download token returned by the request endpoint.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 OK — PDF file stream (<c>application/pdf</c>).<br/>
    /// 404 Not Found — token not found, expired, or report not yet ready.
    /// </returns>
    [HttpGet("pdf/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(
        Guid              patientId,
        [FromQuery] Guid  token,
        CancellationToken ct)
    {
        var result = await _sender.Send(
            new DownloadAppointmentReportQuery(patientId, token), ct);

        return File(result.Bytes, "application/pdf", result.Filename);
    }
}
