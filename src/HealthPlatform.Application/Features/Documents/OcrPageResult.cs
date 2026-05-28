namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Represents the OCR output for a single page or region within a document.
/// Serialised to JSON and stored in <c>ClinicalDocument.ExtractedText</c>.
/// </summary>
public sealed record OcrPageResult(
    int PageNumber,
    string Text,
    double ConfidenceScore
);
