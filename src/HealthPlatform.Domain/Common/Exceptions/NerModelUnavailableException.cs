namespace HealthPlatform.Domain.Common.Exceptions;

/// <summary>
/// Thrown when the Python AI service NER model is temporarily unavailable (HTTP 503).
/// The Hangfire worker will automatically retry the job.
/// </summary>
public sealed class NerModelUnavailableException : Exception
{
    public NerModelUnavailableException()
        : base("NER model is unavailable. The job will be retried automatically.") { }

    public NerModelUnavailableException(string message) : base(message) { }

    public NerModelUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
