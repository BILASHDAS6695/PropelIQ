using HealthPlatform.Application.Features.Documents;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Hangfire fire-and-forget job dispatching <see cref="ProcessDocumentNerCommand"/>
/// for a single document after successful OCR.
/// </summary>
public sealed class DocumentNerJob
{
    private readonly IServiceScopeFactory    _scopeFactory;
    private readonly ILogger<DocumentNerJob> _logger;

    public DocumentNerJob(
        IServiceScopeFactory    scopeFactory,
        ILogger<DocumentNerJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <summary>Entry point invoked by Hangfire.</summary>
    public async Task ExecuteAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("DocumentNerJob started for document {DocumentId}.", documentId);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var sender            = scope.ServiceProvider.GetRequiredService<ISender>();

        await sender.Send(new ProcessDocumentNerCommand(documentId), ct);

        _logger.LogInformation("DocumentNerJob completed for document {DocumentId}.", documentId);
    }
}
