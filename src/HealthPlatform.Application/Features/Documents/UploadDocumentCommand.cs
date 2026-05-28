using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Uploads an encrypted clinical document for a patient.
/// The caller must pass an open, readable stream;
/// the handler disposes nothing — callers own stream lifetime.
/// </summary>
public sealed record UploadDocumentCommand(
    Guid PatientId,
    string OriginalFileName,
    string MimeType,
    long FileSizeBytes,
    Stream FileContent
) : IRequest<DocumentUploadResultDto>;
