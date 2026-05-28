using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Staff-triggered command: creates a blank Draft IntakeRecord for a walk-in patient
/// so that the patient can complete intake at the clinic kiosk or front-desk tablet.
/// Idempotent: if a Draft already exists for this appointment, returns its ID.
/// </summary>
public record TriggerWalkInIntakeCommand(Guid AppointmentId, Guid StaffUserId) : IRequest<Guid>;
