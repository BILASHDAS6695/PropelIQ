namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Carries the OCR extraction result for a single clinical document.
/// </summary>
public sealed record DocumentOcrResultDto(
    Guid DocumentId,
    string FileName,
    string ProcessingStatus,
    double? OcrConfidenceScore,
    IReadOnlyList<OcrPageResult> Pages
);
