using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Domain.Common.Exceptions;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Sends page texts to the Python AI service for NER extraction.
/// Implementations live in the Infrastructure layer (ADR-004).
/// </summary>
public interface INerClient
{
    /// <summary>
    /// Extracts named entities from the provided page texts.
    /// </summary>
    /// <param name="pages">One entry per OCR-extracted document page.</param>
    /// <param name="confidenceThreshold">Entities below this score are flagged low_confidence.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Flat list of all entities across all pages.</returns>
    /// <exception cref="NerModelUnavailableException">
    /// Thrown when the AI service returns 503 — the Hangfire job should retry.
    /// </exception>
    Task<IReadOnlyList<NerEntity>> ExtractAsync(
        IReadOnlyList<string> pages,
        double confidenceThreshold,
        CancellationToken ct);
}
