using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Jobs;

namespace HealthPlatform.Infrastructure.Documents;

/// <summary>
/// Enqueues <see cref="DocumentNerJob"/> via Hangfire fire-and-forget.
/// </summary>
internal sealed class HangfireNerJobScheduler : INerJobScheduler
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireNerJobScheduler(IBackgroundJobClient jobs)
    {
        _jobs = jobs;
    }

    public void Enqueue(Guid documentId)
    {
        _jobs.Enqueue<DocumentNerJob>(
            job => job.ExecuteAsync(documentId, CancellationToken.None));
    }
}
