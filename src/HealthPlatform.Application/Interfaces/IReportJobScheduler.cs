namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Enqueues background jobs for PDF report generation.
/// Implementations live in the Infrastructure layer and interact with
/// Hangfire directly.
/// </summary>
public interface IReportJobScheduler
{
    /// <summary>
    /// Enqueues a Hangfire job to generate the PDF report for
    /// <paramref name="pdfReportId"/> asynchronously.
    /// </summary>
    void EnqueueGenerate(Guid pdfReportId);
}
