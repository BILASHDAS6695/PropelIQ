using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Jobs;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// Enqueues <see cref="DocumentOcrJob"/> via Hangfire's fire-and-forget mechanism.
/// </summary>
internal sealed class HangfireOcrJobScheduler : IOcrJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireOcrJobScheduler(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void Enqueue(Guid documentId)
    {
        _jobs.Enqueue<DocumentOcrJob>(
            job => job.ExecuteAsync(documentId, CancellationToken.None));
    }
}
