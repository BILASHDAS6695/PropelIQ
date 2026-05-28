using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Returns the OCR result for a specific clinical document.
/// <paramref name="PatientId"/> is the JWT User.Id used to verify ownership before returning data.
/// </summary>
public sealed record GetDocumentOcrResultQuery(
    Guid PatientId,
    Guid DocumentId
) : IRequest<DocumentOcrResultDto>;
