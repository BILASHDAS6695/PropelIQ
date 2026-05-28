using System.Text.Json;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class GetDocumentOcrResultQueryHandler
    : IRequestHandler<GetDocumentOcrResultQuery, DocumentOcrResultDto>
{
    private readonly IUnitOfWork                                _uow;
    private readonly ILogger<GetDocumentOcrResultQueryHandler>  _logger;

    public GetDocumentOcrResultQueryHandler(
        IUnitOfWork                               uow,
        ILogger<GetDocumentOcrResultQueryHandler> logger)
    {
        _uow    = uow;
        _logger = logger;
    }

    public async Task<DocumentOcrResultDto> Handle(
        GetDocumentOcrResultQuery query,
        CancellationToken ct)
    {
        // 1. Resolve PatientProfile.Id from User.Id (route param carries User.Id)
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

        // 3. Deserialise stored OCR pages (null-safe)
        IReadOnlyList<OcrPageResult> pages = [];
        if (!string.IsNullOrEmpty(document.ExtractedText))
        {
            pages = JsonSerializer.Deserialize<IReadOnlyList<OcrPageResult>>(
                document.ExtractedText) ?? [];
        }

        _logger.LogInformation(
            "OCR result fetched for document {DocumentId}, patient profile {ProfileId}.",
            query.DocumentId, profileId);

        return new DocumentOcrResultDto(
            document.Id,
            document.FileName,
            document.ProcessingStatus.ToString(),
            document.OcrConfidenceScore,
            pages);
    }
}
