using MediatR;

namespace HealthPlatform.Application.Features.Documents;

public sealed record GetPatientDocumentsQuery(Guid PatientId)
    : IRequest<IReadOnlyList<DocumentSummaryDto>>;
