using System.Net.Http.Json;
using HealthPlatform.Application.Features.Intake;
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

    public IntakeController(
        IHttpClientFactory httpClientFactory,
        ILogger<IntakeController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
}
