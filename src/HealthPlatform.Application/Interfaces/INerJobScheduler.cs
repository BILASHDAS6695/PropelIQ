namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Enqueues background NER jobs for clinical documents.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface INerJobScheduler
{
    /// <summary>
    /// Enqueues a fire-and-forget NER job for <paramref name="documentId"/>.
    /// </summary>
    void Enqueue(Guid documentId);
}
