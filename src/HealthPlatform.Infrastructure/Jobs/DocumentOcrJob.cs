using HealthPlatform.Application.Features.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire fire-and-forget job that dispatches <see cref="ProcessDocumentOcrCommand"/>
/// for a single clinical document after a successful upload.
/// Enqueued by <see cref="HealthPlatform.Infrastructure.Documents.HangfireOcrJobScheduler"/>.
/// </summary>
public sealed class DocumentOcrJob
{
    private readonly IServiceScopeFactory     _scopeFactory;
    private readonly ILogger<DocumentOcrJob>  _logger;

    public DocumentOcrJob(
        IServiceScopeFactory    scopeFactory,
        ILogger<DocumentOcrJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>Entry point invoked by Hangfire.</summary>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("DocumentOcrJob started for document {DocumentId}.", documentId);

        await using var scope  = _scopeFactory.CreateAsyncScope();
        var sender             = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ProcessDocumentOcrCommand(documentId), ct);

        _logger.LogInformation("DocumentOcrJob completed for document {DocumentId}.", documentId);
    }
}
