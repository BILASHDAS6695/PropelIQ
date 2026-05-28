using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetDocumentRawFileQueryHandler
    : IRequestHandler<GetDocumentRawFileQuery, RawDocumentFile>
{
    private readonly IUnitOfWork                             _uow;
    private readonly IDocumentStorageService                 _storage;
    private readonly ILogger<GetDocumentRawFileQueryHandler> _logger;

    public GetDocumentRawFileQueryHandler(
        IUnitOfWork                             uow,
        IDocumentStorageService                 storage,
        ILogger<GetDocumentRawFileQueryHandler> logger)
    {
        _uow     = uow;
        _storage = storage;
        _logger  = logger;
    }

    public async Task<RawDocumentFile> Handle(
        GetDocumentRawFileQuery query,
        CancellationToken ct)
    {
        // 1. Resolve PatientProfile.Id from User.Id
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(query.PatientId), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), query.PatientId);

        var profileId = profiles[0].Id;

        // 2. Load document and verify ownership
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(query.DocumentId, ct)
            ?? throw new NotFoundException(nameof(ClinicalDocument), query.DocumentId);

        if (document.PatientId != profileId)
            throw new ForbiddenAccessException();

        _logger.LogInformation(
            "Raw file stream requested for document {DocumentId}.", query.DocumentId);

        // 3. Decrypt and stream
        var stream = await _storage.ReadAsync(
            document.StoragePath, document.EncryptionIv, ct);

        return new RawDocumentFile(stream, document.MimeType, document.FileName);
    }
}
