using MediatR;

namespace HealthPlatform.Application.Features.Intake;

public record MarkIntakeReviewedCommand(Guid AppointmentId, Guid ReviewerUserId) : IRequest;
