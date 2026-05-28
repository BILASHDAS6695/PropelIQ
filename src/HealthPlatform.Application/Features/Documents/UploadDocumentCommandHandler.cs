using System.Text.Json;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Features.Documents;

internal sealed class UploadDocumentCommandHandler
    : IRequestHandler<UploadDocumentCommand, DocumentUploadResultDto>
{
    private readonly IUnitOfWork _uow;
    private readonly IDocumentStorageService _storage;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UploadDocumentCommandHandler> _logger;

    public UploadDocumentCommandHandler(
        IUnitOfWork uow,
        IDocumentStorageService storage,
        ICurrentUserService currentUser,
        ILogger<UploadDocumentCommandHandler> logger)
    {
        _uow = uow;
        _storage = storage;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<DocumentUploadResultDto> Handle(
        UploadDocumentCommand command,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("Authentication required to upload documents.");

        // 1. Resolve patient profile by User.Id (the route param carries the User.Id)
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(command.PatientId), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), command.PatientId);

        var profileId = profiles[0].Id;

        // 2. Encrypt and write file to local storage
        var (storagePath, encryptionIv) = await _storage.SaveAsync(
            command.OriginalFileName,
            command.FileContent,
            ct);

        // 3. Persist DB record (rollback file on failure)
        var now = DateTimeOffset.UtcNow;
        var document = new ClinicalDocument
        {
            PatientId        = profileId,
            FileName         = command.OriginalFileName,
            MimeType         = command.MimeType,
            StoragePath      = storagePath,
            FileSizeBytes    = command.FileSizeBytes,
            UploadedAt       = now,
            ProcessingStatus = DocumentProcessingStatus.Uploaded,
            EncryptionIv     = encryptionIv,
        };

        try
        {
            await _uow.Repository<ClinicalDocument>().AddAsync(document, ct);

            // 4. Audit log
            var auditEntry = new AuditLog
            {
                UserId     = _currentUser.UserId.Value,
                Action     = "DocumentUploaded",
                EntityType = nameof(ClinicalDocument),
                EntityId   = document.Id,
                Timestamp  = now,
                Details    = JsonDocument.Parse(
                    $$$"""
                    {
                      "fileName":  "{{{command.OriginalFileName}}}",
                      "mimeType":  "{{{command.MimeType}}}",
                      "sizeBytes": {{{command.FileSizeBytes}}},
                      "patientId": "{{{command.PatientId}}}"
                    }
                    """),
            };
            await _uow.Repository<AuditLog>().AddAsync(auditEntry, ct);

            await _uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Partial upload: remove the already-written encrypted file
            _storage.Delete(storagePath);
            _logger.LogError(ex, "DB persist failed after writing document to disk. File cleaned up.");
            throw;
        }

        _logger.LogInformation(
            "Document {DocumentId} uploaded for patient {PatientId} ({FileName})",
            document.Id, command.PatientId, command.OriginalFileName);

        return new DocumentUploadResultDto(
            document.Id,
            document.FileName,
            document.MimeType,
            document.FileSizeBytes,
            document.UploadedAt,
            document.ProcessingStatus.ToString());
    }
}
