namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Enqueues background OCR jobs for clinical documents.
/// Implementations live in the Infrastructure layer and interact with
/// Hangfire's <c>IBackgroundJobClient</c> directly.
/// </summary>
public interface IOcrJobScheduler
{
    /// <summary>
    /// Enqueues a fire-and-forget OCR job for <paramref name="documentId"/>.
    /// The job runs as soon as a Hangfire worker is available.
    /// </summary>
    void Enqueue(Guid documentId);
}
