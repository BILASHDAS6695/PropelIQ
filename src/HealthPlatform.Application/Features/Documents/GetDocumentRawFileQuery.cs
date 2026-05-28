using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Returns the decrypted raw file stream for a clinical document.
/// Used by the Angular document viewer to display the original PDF or image.
/// </summary>
public sealed record GetDocumentRawFileQuery(Guid PatientId, Guid DocumentId)
    : IRequest<RawDocumentFile>;

/// <summary>Carries the decrypted file stream returned to the caller.</summary>
public sealed record RawDocumentFile(
    Stream FileStream,
    string ContentType,
    string FileName
);
