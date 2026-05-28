using MediatR;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>Returns whether the intake window is open for the given appointment.</summary>
public record GetIntakeWindowQuery(Guid AppointmentId)
    : IRequest<IntakeWindowResult?>;

/// <param name="IsOpen">True when the patient may access the intake form.</param>
/// <param name="Reason">Human-readable reason when <see cref="IsOpen"/> is false; null when open.</param>
public record IntakeWindowResult(bool IsOpen, string? Reason);
