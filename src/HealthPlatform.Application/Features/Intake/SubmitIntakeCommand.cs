using MediatR;

namespace HealthPlatform.Application.Features.Intake;

public record SubmitIntakeCommand(
    Guid AppointmentId,
    Guid PatientUserId,
    Domain.Enums.IntakeMode Mode,
    Domain.ValueObjects.IntakeData Data) : IRequest<Guid>;
