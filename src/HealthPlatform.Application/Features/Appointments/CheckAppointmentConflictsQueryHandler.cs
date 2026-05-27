using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Handles <see cref="CheckAppointmentConflictsQuery"/>.
///
/// Flow:
///  1. Resolve patient profile from UserId.
///  2. Load the requested slot to obtain the proposed SlotTime.
///  3. Load all non-terminal appointments for the patient on that calendar day
///     (across all providers) via PatientActiveSameDayAppointmentsSpecification.
///  4. Classify: hard if |SlotTime delta| &lt; 30 min; soft otherwise.
///  5. Return the worst conflict found (hard &gt; soft &gt; none), with details of
///     the first conflicting appointment for UI display.
/// </summary>
internal sealed class CheckAppointmentConflictsQueryHandler
    : IRequestHandler<CheckAppointmentConflictsQuery, ConflictCheckResultDto>
{
    private const int HardConflictWindowMinutes = 30;

    private readonly IUnitOfWork _uow;

    public CheckAppointmentConflictsQueryHandler(IUnitOfWork uow) => _uow = uow;

    public async Task<ConflictCheckResultDto> Handle(
        CheckAppointmentConflictsQuery query,
        CancellationToken              ct)
    {
        // ── 1. Resolve patient profile ────────────────────────────────────
        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(query.UserId), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), query.UserId);

        var patientId = profiles[0].Id;

        // ── 2. Load slot ───────────────────────────────────────────────────
        var slot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(query.SlotId, ct)
            ?? throw new NotFoundException(nameof(AppointmentSlot), query.SlotId);

        var proposedTime = slot.StartTime;
        var proposedDate = DateOnly.FromDateTime(proposedTime.UtcDateTime);

        // ── 3. Load same-day active appointments ──────────────────────────
        var existing = await _uow.Repository<Appointment>()
            .GetAsync(
                new PatientActiveSameDayAppointmentsSpecification(patientId, proposedDate),
                ct);

        if (existing.Count == 0)
            return new ConflictCheckResultDto("None", null, null, null, null);

        // ── 4. Classify ───────────────────────────────────────────────────
        // Hard: |delta| < 30 min
        var hardConflict = existing.FirstOrDefault(
            a => Math.Abs((a.SlotTime - proposedTime).TotalMinutes) < HardConflictWindowMinutes);

        if (hardConflict is not null)
            return new ConflictCheckResultDto(
                "Hard",
                hardConflict.Id,
                hardConflict.Provider.Name,
                hardConflict.SlotTime,
                $"You already have an appointment with {hardConflict.Provider.Name} at " +
                $"{hardConflict.SlotTime:t} UTC on the same day. " +
                "These appointments overlap within a 30-minute window.");

        // Soft: same day, outside hard window
        var softConflict = existing[0];
        return new ConflictCheckResultDto(
            "Soft",
            softConflict.Id,
            softConflict.Provider.Name,
            softConflict.SlotTime,
            $"You have another appointment with {softConflict.Provider.Name} at " +
            $"{softConflict.SlotTime:t} UTC on the same day. " +
            "You can still proceed with this booking.");
    }
}
