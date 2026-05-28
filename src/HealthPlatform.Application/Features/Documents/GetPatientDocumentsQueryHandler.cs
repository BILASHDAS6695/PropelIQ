using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetPatientDocumentsQueryHandler
    : IRequestHandler<GetPatientDocumentsQuery, IReadOnlyList<DocumentSummaryDto>>
{
    private readonly IUnitOfWork _uow;

    public GetPatientDocumentsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<IReadOnlyList<DocumentSummaryDto>> Handle(
        GetPatientDocumentsQuery query,
        CancellationToken ct)
    {
        // Resolve the PatientProfile.Id from the User.Id carried in the route param
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(query.PatientId), ct);

        if (profiles.Count == 0)
            return [];

        var profileId = profiles[0].Id;
        var spec = new PatientDocumentsByPatientIdSpecification(profileId);
        var docs = await _uow.Repository<ClinicalDocument>().GetAsync(spec, ct);

        return docs
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentSummaryDto(
                d.Id,
                d.FileName,
                d.MimeType,
                d.FileSizeBytes,
                d.UploadedAt,
                d.ProcessingStatus.ToString()))
            .ToList();
    }
}
