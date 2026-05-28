using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class ProcessDocumentNerCommandHandler : IRequestHandler<ProcessDocumentNerCommand>
{
    private readonly IUnitOfWork                               _uow;
    private readonly INerClient                                _nerClient;
    private readonly NerSettings                               _settings;
    private readonly ILogger<ProcessDocumentNerCommandHandler> _logger;

    public ProcessDocumentNerCommandHandler(
        IUnitOfWork                               uow,
        INerClient                                nerClient,
        IOptions<NerSettings>                     settings,
        ILogger<ProcessDocumentNerCommandHandler> logger)
    {
        _uow       = uow;
        _nerClient = nerClient;
        _settings  = settings.Value;
        _logger    = logger;
    }

    public async Task Handle(ProcessDocumentNerCommand command, CancellationToken ct)
    {
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(command.DocumentId, ct);

        if (document is null)
        {
            _logger.LogWarning("NER skipped — document {DocumentId} not found.", command.DocumentId);
            return;
        }

        // If OCR failed, NER should not run.
        if (document.ProcessingStatus == DocumentProcessingStatus.Failed)
        {
            _logger.LogWarning(
                "NER skipped — document {DocumentId} is in Failed state.", command.DocumentId);
            return;
        }

        // Build page list from OCR output (empty list = no text available).
        IReadOnlyList<string> pages = [];
        if (!string.IsNullOrEmpty(document.ExtractedText))
        {
            var ocrPages = JsonSerializer.Deserialize<IReadOnlyList<OcrPageResult>>(document.ExtractedText);
            pages = ocrPages?.Select(p => p.Text).ToList() ?? [];
        }

        try
        {
            if (pages.Count == 0 || pages.All(p => string.IsNullOrWhiteSpace(p)))
            {
                // No text to process — store empty entities array, mark Processed.
                _logger.LogInformation(
                    "NER skipped (no extracted text) for document {DocumentId}. Marking Processed.",
                    command.DocumentId);
                document.Entities         = "[]";
                document.ProcessingStatus = DocumentProcessingStatus.Processed;
                await _uow.SaveChangesAsync(ct);
                return;
            }

            var entities = await _nerClient.ExtractAsync(
                pages, _settings.ConfidenceThreshold, ct);

            document.Entities         = JsonSerializer.Serialize(entities);
            document.ProcessingStatus = DocumentProcessingStatus.Processed;

            _logger.LogInformation(
                "NER completed for document {DocumentId}. Entities={Count}.",
                command.DocumentId, entities.Count);
        }
        catch (NerModelUnavailableException ex)
        {
            // Re-throw so Hangfire retries the job; status stays Processing.
            _logger.LogWarning(ex, "NER model unavailable for document {DocumentId}. Job will be retried.",
                command.DocumentId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NER extraction failed for document {DocumentId}.", command.DocumentId);
            document.ProcessingStatus = DocumentProcessingStatus.Failed;
        }

        await _uow.SaveChangesAsync(ct);
    }
}
