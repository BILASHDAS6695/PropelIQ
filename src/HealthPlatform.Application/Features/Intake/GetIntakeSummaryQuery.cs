using MediatR;

namespace HealthPlatform.Application.Features.Intake;

public record GetIntakeSummaryQuery(Guid AppointmentId) : IRequest<IntakeSummaryDto?>;
