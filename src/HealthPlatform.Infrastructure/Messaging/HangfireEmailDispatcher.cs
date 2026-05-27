using Hangfire;
using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// IEmailSender implementation that enqueues a Hangfire background job
/// instead of delivering inline. The HTTP request returns immediately;
/// <see cref="SendEmailJob"/> runs on the background worker thread.
/// </summary>
internal sealed class HangfireEmailDispatcher : IEmailSender
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireEmailDispatcher(IBackgroundJobClient jobs) => _jobs = jobs;

    public Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        _jobs.Enqueue<SendEmailJob>(job => job.ExecuteAsync(toAddress, subject, body));
        return Task.CompletedTask;
    }
}
