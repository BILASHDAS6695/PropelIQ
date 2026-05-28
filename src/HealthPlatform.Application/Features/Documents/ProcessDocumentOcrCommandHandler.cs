using System.Text.Json;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class ProcessDocumentOcrCommandHandler : IRequestHandler<ProcessDocumentOcrCommand>
{
    private readonly IUnitOfWork                               _uow;
    private readonly IDocumentStorageService                   _storage;
    private readonly IOcrService                               _ocr;
    private readonly TesseractSettings                         _settings;
    private readonly INerJobScheduler                          _nerScheduler;
    private readonly ILogger<ProcessDocumentOcrCommandHandler> _logger;

    public ProcessDocumentOcrCommandHandler(
        IUnitOfWork                               uow,
        IDocumentStorageService                   storage,
        IOcrService                               ocr,
        IOptions<TesseractSettings>               settings,
        INerJobScheduler                          nerScheduler,
        ILogger<ProcessDocumentOcrCommandHandler> logger)
    {
        _uow          = uow;
        _storage      = storage;
        _ocr          = ocr;
        _settings     = settings.Value;
        _nerScheduler = nerScheduler;
        _logger       = logger;
    }

    public async Task Handle(ProcessDocumentOcrCommand command, CancellationToken ct)
    {
        var document = await _uow.Repository<ClinicalDocument>()
            .GetByIdAsync(command.DocumentId, ct);

        if (document is null)
        {
            _logger.LogWarning("OCR skipped — document {DocumentId} not found.", command.DocumentId);
            return;
        }

        // Mark as Processing so the UI can show an in-progress state.
        document.ProcessingStatus = DocumentProcessingStatus.Processing;
        await _uow.SaveChangesAsync(ct);

        try
        {
            await using var fileStream = await _storage.ReadAsync(
                document.StoragePath, document.EncryptionIv, ct);

            var pages = await _ocr.ExtractAsync(fileStream, document.MimeType, ct);

            var avgConfidence = pages.Count > 0
                ? pages.Average(p => p.ConfidenceScore)
                : 0.0;

            if (avgConfidence < _settings.MinimumConfidenceThreshold && pages.Count > 0)
            {
                _logger.LogWarning(
                    "OCR confidence {Score:F1}% below threshold for document {DocumentId}. Marking Failed.",
                    avgConfidence, command.DocumentId);

                document.ProcessingStatus  = DocumentProcessingStatus.Failed;
                document.OcrConfidenceScore = avgConfidence;
            }
            else
            {
                document.ExtractedText      = JsonSerializer.Serialize(pages);
                document.OcrConfidenceScore = avgConfidence;
                // ProcessingStatus intentionally stays Processing — NER job sets Processed.

                _logger.LogInformation(
                    "OCR completed for document {DocumentId}. Pages={PageCount}, Confidence={Score:F1}%. Enqueueing NER.",
                    command.DocumentId, pages.Count, avgConfidence);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OCR extraction failed for document {DocumentId}.", command.DocumentId);
            document.ProcessingStatus = DocumentProcessingStatus.Failed;
        }

        await _uow.SaveChangesAsync(ct);

        // Enqueue NER only when OCR succeeded (status still Processing after save).
        if (document.ProcessingStatus == DocumentProcessingStatus.Processing
            && !string.IsNullOrEmpty(document.ExtractedText))
        {
            _nerScheduler.Enqueue(document.Id);
        }
    }
}
