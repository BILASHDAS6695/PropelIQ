using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class ClinicalDocument : AuditableEntity
{
    public Guid PatientId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DocumentProcessingStatus ProcessingStatus { get; set; }

    /// <summary>
    /// Hex-encoded 16-byte AES-IV used to encrypt this file.
    /// The master encryption key lives in DocumentStorage:EncryptionKey (config).
    /// </summary>
    public string EncryptionIv { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of OcrPageResult objects: [{ pageNumber, text, confidenceScore }].
    /// Null until OCR job completes successfully.
    /// Stored as a PostgreSQL JSONB column for efficient querying.
    /// </summary>
    public string? ExtractedText { get; set; }

    /// <summary>
    /// Average OCR confidence score across all pages (0.0–100.0).
    /// Null until OCR job completes successfully.
    /// </summary>
    public double? OcrConfidenceScore { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ExtractedData> ExtractedData { get; set; } = [];
}
