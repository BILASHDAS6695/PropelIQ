namespace HealthPlatform.Application.Features.Documents;

public sealed record DocumentUploadResultDto(
    Guid DocumentId,
    string FileName,
    string MimeType,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    string ProcessingStatus
);
