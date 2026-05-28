using HealthPlatform.Application.Features.Documents;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Extracts text from clinical documents using OCR.
/// All processing runs locally (TR-026 / ADR-004).
/// </summary>
public interface IOcrService
{
    /// <summary>
    /// Extracts text from the given document stream.
    /// </summary>
    /// <param name="fileStream">
    /// A readable, seekable stream of the decrypted document bytes.
    /// </param>
    /// <param name="mimeType">
    /// MIME type of the document (e.g., <c>application/pdf</c>, <c>image/png</c>).
    /// Determines whether PdfPig or Tesseract is used as the primary extractor.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A list of <see cref="OcrPageResult"/> — one entry per page/region.
    /// Never returns null; returns an empty list on total failure.
    /// </returns>
    Task<IReadOnlyList<OcrPageResult>> ExtractAsync(
        Stream fileStream,
        string mimeType,
        CancellationToken ct);
}
