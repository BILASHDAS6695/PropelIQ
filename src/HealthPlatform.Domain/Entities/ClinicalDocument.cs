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

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<ExtractedData> ExtractedData { get; set; } = [];
}
