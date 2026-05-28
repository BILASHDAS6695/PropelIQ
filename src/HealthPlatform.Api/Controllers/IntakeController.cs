using System.Net.Http.Json;
using HealthPlatform.Application.Features.Intake;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IntakeController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntakeController> _logger;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public IntakeController(
        IHttpClientFactory httpClientFactory,
        ILogger<IntakeController> logger,
        ISender sender,
        ICurrentUserService currentUser)
    {
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
        _sender            = sender;
        _currentUser       = currentUser;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<IntakeChatProxyResponse>> Chat(
        [FromBody] IntakeChatProxyRequest request,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AiService");
        try
        {
            var aiResponse = await client.PostAsJsonAsync(
                "/intake/chat", request, cancellationToken);

            if (!aiResponse.IsSuccessStatusCode)
            {
                var status = (int)aiResponse.StatusCode;
                _logger.LogWarning(
                    "AI service returned {StatusCode} for intake/chat", status);
                return StatusCode(status, new { detail = "Upstream AI service error." });
            }

            var result = await aiResponse.Content
                .ReadFromJsonAsync<IntakeChatProxyResponse>(cancellationToken: cancellationToken);
            return Ok(result);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("AI service timeout on intake/chat");
            return StatusCode(504, new { detail = "AI service timed out." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI service unreachable on intake/chat");
            return StatusCode(503, new { detail = "AI service unavailable." });
        }
    }

    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft(
        [FromBody] SaveIntakeDraftRequest request,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Unauthorized();

        var cmd = new SaveIntakeDraftCommand(
            request.AppointmentId,
            _currentUser.UserId.Value,
            request.Mode,
            request.Data);

        var id = await _sender.Send(cmd, ct);
        return Ok(new { id });
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitIntakeRequest request,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Unauthorized();

        var cmd = new SubmitIntakeCommand(
            request.AppointmentId,
            _currentUser.UserId.Value,
            request.Mode,
            request.Data);

        var id = await _sender.Send(cmd, ct);
        return Ok(new { id });
    }

    [HttpGet("{appointmentId:guid}")]
    public async Task<ActionResult<IntakeSummaryDto>> GetSummary(
        Guid appointmentId,
        CancellationToken ct)
    {
        var result = await _sender.Send(new GetIntakeSummaryQuery(appointmentId), ct);
        if (result is null) return NotFound();

        if (result.Status == IntakeStatus.Draft &&
            HttpContext.User.IsInRole("Provider"))
            Response.Headers.Append("X-Intake-Warning", "Intake not completed by patient");

        return Ok(result);
    }

    [HttpPut("{appointmentId:guid}/reviewed")]
    public async Task<IActionResult> MarkReviewed(
        Guid appointmentId,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            return Unauthorized();

        await _sender.Send(
            new MarkIntakeReviewedCommand(appointmentId, _currentUser.UserId.Value), ct);
        return NoContent();
    }
}
