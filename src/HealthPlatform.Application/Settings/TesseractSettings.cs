namespace HealthPlatform.Application.Settings;

public sealed class TesseractSettings
{
    public const string SectionName = "Tesseract";

    /// <summary>
    /// Absolute path to the directory containing Tesseract language data files
    /// (*.traineddata). Defaults to /usr/share/tessdata in Docker.
    /// Override with environment variable: Tesseract__TessDataPath
    /// </summary>
    public string TessDataPath { get; init; } = "/usr/share/tessdata";

    /// <summary>
    /// Tesseract language code to use (e.g., "eng" for English).
    /// </summary>
    public string Language { get; init; } = "eng";

    /// <summary>
    /// Minimum average confidence score (0–100) below which the document
    /// is flagged as low-quality and status is set to Failed.
    /// </summary>
    public double MinimumConfidenceThreshold { get; init; } = 30.0;

    /// <summary>
    /// Minimum character count of embedded PDF text before treating the PDF
    /// as text-based (and skipping OCR). PDFs with fewer characters are treated
    /// as scanned and passed to Tesseract.
    /// </summary>
    public int PdfEmbeddedTextMinLength { get; init; } = 50;
}
