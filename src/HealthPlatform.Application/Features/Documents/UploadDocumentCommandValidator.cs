using FluentValidation;

namespace HealthPlatform.Application.Features.Documents;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/tiff",
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".tiff", ".tif",
    };

    // Magic bytes for file-type validation (first-line defence against content-type spoofing)
    private static readonly Dictionary<string, byte[][]> MagicBytes = new()
    {
        ["application/pdf"] = [[0x25, 0x50, 0x44, 0x46]],           // %PDF
        ["image/png"]       = [[0x89, 0x50, 0x4E, 0x47]],           // ‰PNG
        ["image/jpeg"]      = [[0xFF, 0xD8, 0xFF]],                  // JFIF/EXIF
        ["image/tiff"]      = [[0x49, 0x49, 0x2A, 0x00],            // II*\0 (little-endian)
                               [0x4D, 0x4D, 0x00, 0x2A]],           // MM\0* (big-endian)
    };

    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty();

        RuleFor(x => x.OriginalFileName)
            .NotEmpty()
            .MaximumLength(500)
            .Must(name =>
            {
                var ext = Path.GetExtension(name);
                return AllowedExtensions.Contains(ext);
            })
            .WithMessage(x =>
            {
                var ext = Path.GetExtension(x.OriginalFileName);
                return $"Unsupported format: {ext}. Accepted: PDF, PNG, JPG, TIFF";
            });

        RuleFor(x => x.MimeType)
            .NotEmpty()
            .Must(m => AllowedMimeTypes.Contains(m))
            .WithMessage(x => $"Unsupported MIME type: {x.MimeType}");

        RuleFor(x => x.FileSizeBytes)
            .InclusiveBetween(1, MaxBytes)
            .WithMessage("File too large. Maximum size: 10 MB");

        RuleFor(x => x.FileContent)
            .NotNull()
            .Must((cmd, stream) => ValidateMagicBytes(cmd.MimeType, stream))
            .WithMessage("File content does not match the declared type (magic-byte mismatch)");
    }

    private static bool ValidateMagicBytes(string mimeType, Stream stream)
    {
        if (!MagicBytes.TryGetValue(mimeType, out var signatures))
            return false;

        const int ReadLen = 4;
        var header = new byte[ReadLen];
        var read = stream.Read(header, 0, ReadLen);
        stream.Seek(0, SeekOrigin.Begin); // reset for subsequent reads

        return signatures.Any(sig =>
            read >= sig.Length &&
            header.Take(sig.Length).SequenceEqual(sig));
    }
}
