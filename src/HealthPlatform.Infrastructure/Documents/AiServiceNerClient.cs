using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// Calls the Python AI service <c>POST /extraction/ner</c> endpoint.
/// </summary>
internal sealed class AiServiceNerClient : INerClient
{
    private readonly HttpClient                  _http;
    private readonly NerSettings                 _settings;
    private readonly ILogger<AiServiceNerClient> _logger;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public AiServiceNerClient(
        HttpClient                  http,
        IOptions<NerSettings>       settings,
        ILogger<AiServiceNerClient> logger)
    {
        _http     = http;
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task<IReadOnlyList<NerEntity>> ExtractAsync(
        IReadOnlyList<string> pages,
        double confidenceThreshold,
        CancellationToken ct)
    {
        var requestBody = new
        {
            pages                = pages,
            confidence_threshold = confidenceThreshold,
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/extraction/ner", requestBody, _json, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to AI service NER endpoint failed.");
            throw new NerModelUnavailableException("AI service is unreachable.", ex);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            _logger.LogWarning("AI service returned 503 — NER model unavailable.");
            throw new NerModelUnavailableException();
        }

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<NerApiResponse>(_json, ct)
            ?? throw new InvalidOperationException("AI service returned null NER response.");

        return result.Entities
            .Select(e => new NerEntity(
                e.Text,
                e.Type,
                e.StartOffset,
                e.EndOffset,
                e.ConfidenceScore,
                e.LowConfidence))
            .ToList();
    }

    // ── Private response DTOs (match AI service JSON shape) ──────────────

    private sealed record NerApiResponse(
        [property: JsonPropertyName("entities")] List<NerEntityDto> Entities
    );

    private sealed record NerEntityDto(
        [property: JsonPropertyName("text")]             string Text,
        [property: JsonPropertyName("type")]             string Type,
        [property: JsonPropertyName("start_offset")]     int    StartOffset,
        [property: JsonPropertyName("end_offset")]       int    EndOffset,
        [property: JsonPropertyName("confidence_score")] double ConfidenceScore,
        [property: JsonPropertyName("low_confidence")]   bool   LowConfidence
    );
}
