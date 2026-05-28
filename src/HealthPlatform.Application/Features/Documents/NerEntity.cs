namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// A single clinical named entity extracted from a document page.
/// Serialised as part of a JSONB array in <c>clinical_documents.entities</c>.
/// </summary>
public sealed record NerEntity(
    /// <summary>Surface text of the entity as it appears in the source.</summary>
    string Text,

    /// <summary>
    /// Normalised entity type: DIAGNOSIS | MEDICATION | PROCEDURE |
    /// LAB_TEST | LAB_VALUE | ANATOMY | SYMPTOM.
    /// </summary>
    string Type,

    /// <summary>Zero-based character start offset within the page text.</summary>
    int StartOffset,

    /// <summary>Zero-based character end offset (exclusive) within the page text.</summary>
    int EndOffset,

    /// <summary>Model confidence score 0.0–1.0.</summary>
    double ConfidenceScore,

    /// <summary>
    /// True when the confidence score is below the configured minimum threshold.
    /// Low-confidence entities are stored but should be treated as unverified.
    /// </summary>
    bool LowConfidence
);
