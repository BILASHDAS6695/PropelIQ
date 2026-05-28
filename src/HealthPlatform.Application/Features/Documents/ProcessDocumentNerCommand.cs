using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Triggers the NER extraction pipeline for a single clinical document.
/// Dispatched by <see cref="HealthPlatform.Infrastructure.Jobs.DocumentNerJob"/>
/// after successful OCR processing.
/// </summary>
public sealed record ProcessDocumentNerCommand(Guid DocumentId) : IRequest;
