using MediatR;

namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// Triggers the OCR extraction pipeline for a single clinical document.
/// Dispatched by <see cref="HealthPlatform.Infrastructure.Jobs.DocumentOcrJob"/> after a successful upload.
/// </summary>
public sealed record ProcessDocumentOcrCommand(Guid DocumentId) : IRequest;
