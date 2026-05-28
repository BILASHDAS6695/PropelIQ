using Hangfire;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Reports;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire-backed implementation of <see cref="IReportJobScheduler"/>.
/// Enqueues <see cref="GeneratePdfReportJob"/> for asynchronous PDF generation.
/// </summary>
internal sealed class HangfireReportJobScheduler : IReportJobScheduler
{
    private readonly IBackgroundJobClient _client;

    public HangfireReportJobScheduler(IBackgroundJobClient client) =>
        _client = client;

    public void EnqueueGenerate(Guid pdfReportId) =>
        _client.Enqueue<GeneratePdfReportJob>(
            job => job.ExecuteAsync(pdfReportId, CancellationToken.None));
}
