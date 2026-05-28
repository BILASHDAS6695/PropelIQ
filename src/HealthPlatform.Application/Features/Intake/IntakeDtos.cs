using HealthPlatform.Domain.Enums;
using HealthPlatform.Domain.ValueObjects;

namespace HealthPlatform.Application.Features.Intake;

public record SaveIntakeDraftRequest(
    Guid AppointmentId,
    IntakeMode Mode,
    IntakeData Data);

public record SubmitIntakeRequest(
    Guid AppointmentId,
    IntakeMode Mode,
    IntakeData Data);

public record IntakeSummaryDto(
    Guid Id,
    Guid AppointmentId,
    Guid PatientId,
    IntakeMode Mode,
    IntakeStatus Status,
    IntakeData? Data,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedByProviderId);
