using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using Tesseract;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// OCR service using Tesseract for images, and PDFtoImage + Tesseract for PDFs.
/// PDFs are rendered page-by-page to bitmaps via Pdfium (PDFtoImage) and then
/// processed by Tesseract. Image files are fed to Tesseract directly.
/// All processing is fully local — no external API calls (TR-026 / ADR-004).
/// </summary>
internal sealed class TesseractOcrService : IOcrService
{
    private readonly TesseractSettings            _settings;
    private readonly ILogger<TesseractOcrService> _logger;

    public TesseractOcrService(
        IOptions<TesseractSettings>  options,
        ILogger<TesseractOcrService> logger)
    {
        _settings = options.Value;
        _logger   = logger;
    }

    public async Task<IReadOnlyList<OcrPageResult>> ExtractAsync(
        Stream fileStream,
        string mimeType,
        CancellationToken ct)
    {
        return mimeType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)
            ? await ExtractFromPdfAsync(fileStream, ct)
            : await ExtractFromImageAsync(fileStream, ct);
    }

    // ── PDF: render each page → Tesseract ────────────────────────────────────

    private async Task<IReadOnlyList<OcrPageResult>> ExtractFromPdfAsync(
        Stream fileStream, CancellationToken ct)
    {
        fileStream.Seek(0, SeekOrigin.Begin);
        var pdfBytes = await ReadAllBytesAsync(fileStream, ct);

        var results = new List<OcrPageResult>();
        int pageNum  = 1;

        using var engine = CreateEngine();

#pragma warning disable CA1416 // PDFtoImage requires Windows/Linux/macOS — this service only runs in ASP.NET Core on those platforms
        foreach (var bitmap in Conversion.ToImages(pdfBytes))
#pragma warning restore CA1416
        {
            ct.ThrowIfCancellationRequested();

            using (bitmap)
            {
                var pngBytes = EncodeBitmapToPng(bitmap);
                results.Add(RunTesseract(engine, pngBytes, pageNum++));
            }

            await Task.Yield(); // avoid blocking thread pool between pages
        }

        _logger.LogInformation(
            "PDF OCR complete: {PageCount} page(s), avg confidence {Confidence:F1}%.",
            results.Count,
            results.Count > 0 ? results.Average(p => p.ConfidenceScore) : 0.0);

        return results;
    }

    // ── Image: Tesseract directly ─────────────────────────────────────────────

    private async Task<IReadOnlyList<OcrPageResult>> ExtractFromImageAsync(
        Stream fileStream, CancellationToken ct)
    {
        await Task.Yield();

        var imageBytes = await ReadAllBytesAsync(fileStream, ct);

        using var engine = CreateEngine();
        return [RunTesseract(engine, imageBytes, pageNumber: 1)];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private TesseractEngine CreateEngine() =>
        new(_settings.TessDataPath, _settings.Language, EngineMode.Default);

    private OcrPageResult RunTesseract(TesseractEngine engine, byte[] imageBytes, int pageNumber)
    {
        try
        {
            using var pix  = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);

            var text       = page.GetText()?.Trim() ?? string.Empty;
            var confidence = (double)page.GetMeanConfidence() * 100.0;

            _logger.LogDebug(
                "OCR page {Page}: {CharCount} chars, confidence {Confidence:F1}%",
                pageNumber, text.Length, confidence);

            return new OcrPageResult(pageNumber, text, Math.Round(confidence, 2));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tesseract failed on page {Page}.", pageNumber);
            return new OcrPageResult(pageNumber, string.Empty, 0.0);
        }
    }

    private static byte[] EncodeBitmapToPng(SKBitmap bitmap)
    {
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return ms.ToArray();
    }
}
