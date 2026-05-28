namespace HealthPlatform.Application.Settings;

public sealed class NerSettings
{
    public const string SectionName = "Ner";

    /// <summary>Base URL of the Python AI service (e.g., http://ai:8000).</summary>
    public string AiServiceBaseUrl { get; init; } = "http://ai:8000";

    /// <summary>Internal API key sent in the X-Internal-Api-Key header.</summary>
    public string InternalApiKey { get; init; } = string.Empty;

    /// <summary>Minimum confidence threshold (0.0–1.0) sent to the NER service.</summary>
    public double ConfidenceThreshold { get; init; } = 0.7;

    /// <summary>HTTP request timeout for the NER call in seconds.</summary>
    public int TimeoutSeconds { get; init; } = 60;
}
