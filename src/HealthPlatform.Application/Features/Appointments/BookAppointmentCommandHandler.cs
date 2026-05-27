using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Books an appointment slot for the currently authenticated patient.
///
/// Flow:
/// 1. Resolve patient profile from ICurrentUserService.UserId.
/// 2. Load the requested slot — verify it is Available.
/// 3. Guard against duplicate active appointments on the same provider/day.
/// 4. Create Appointment (status = Scheduled) + mark slot Booked.
/// 5. SaveChanges — UnitOfWork translates DbUpdateConcurrencyException into
///    ConflictException so the first-wins booking race surfaces as HTTP 409.
/// 6. Send confirmation email via IEmailSender.
/// </summary>
internal sealed class BookAppointmentCommandHandler
    : IRequestHandler<BookAppointmentCommand, BookingConfirmationDto>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IEmailSender        _emailSender;

    public BookAppointmentCommandHandler(
        IUnitOfWork         uow,
        ICurrentUserService currentUser,
        IEmailSender        emailSender)
    {
        _uow         = uow;
        _currentUser = currentUser;
        _emailSender = emailSender;
    }

    public async Task<BookingConfirmationDto> Handle(
        BookAppointmentCommand command,
        CancellationToken      ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated to book appointments.");

        // ── 1. Resolve patient profile ────────────────────────────────────
        var patientProfiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (patientProfiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var patient = patientProfiles[0];

        // ── 2. Load slot ───────────────────────────────────────────────────
        var slot = await _uow.Repository<AppointmentSlot>()
            .GetByIdAsync(command.SlotId, ct)
            ?? throw new NotFoundException(nameof(AppointmentSlot), command.SlotId);

        if (slot.Status != SlotStatus.Available)
            throw new ConflictException("This slot is no longer available.");

        // ── 3. Cross-provider conflict detection (fail fast, before slot lock) ───
        const int HardWindowMinutes = 30;
        var proposedDate = DateOnly.FromDateTime(slot.StartTime.UtcDateTime);

        var sameDayAppointments = await _uow.Repository<Appointment>()
            .GetAsync(
                new PatientActiveSameDayAppointmentsSpecification(patient.Id, proposedDate),
                ct);

        string? conflictWarning = null;

        if (sameDayAppointments.Count > 0)
        {
            var hardConflict = sameDayAppointments.FirstOrDefault(
                a => Math.Abs((a.SlotTime - slot.StartTime).TotalMinutes) < HardWindowMinutes);

            if (hardConflict is not null)
            {
                if (!command.ForceBook)
                    throw new ConflictException(
                        $"Appointment conflict: you have an overlapping appointment with " +
                        $"{hardConflict.Provider.Name} at {hardConflict.SlotTime:t} UTC " +
                        $"(ID: {hardConflict.Id}). Staff can override with ForceBook = true.");

                // Staff override: warning captured in DTO + persisted on entity
                conflictWarning =
                    $"Override: conflicting appointment {hardConflict.Id} with " +
                    $"{hardConflict.Provider.Name} at {hardConflict.SlotTime:t} UTC.";
            }
            else
            {
                // Soft conflict — same day but outside 30-min window
                var softConflict = sameDayAppointments[0];
                conflictWarning =
                    $"Note: you have another appointment with {softConflict.Provider.Name} at " +
                    $"{softConflict.SlotTime:t} UTC on the same day.";
            }
        }

        // ── 4. Load provider for confirmation DTO ──────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(slot.ProviderId, ct)
            ?? throw new NotFoundException(nameof(Provider), slot.ProviderId);

        // ── 5. Create appointment + mark slot Booked ───────────────────────
        var appointment = new Appointment
        {
            Id                     = Guid.NewGuid(),
            PatientId              = patient.Id,
            ProviderId             = slot.ProviderId,
            SlotId                 = slot.Id,
            SlotTime               = slot.StartTime,
            Status                 = AppointmentStatus.Scheduled,
            VisitReason            = command.VisitReason,
            IsWalkIn               = false,
            IsConflictOverride     = command.ForceBook && conflictWarning is not null,
            ConflictOverrideReason = command.OverrideReason
        };

        slot.Status = SlotStatus.Booked;
        _uow.Repository<AppointmentSlot>().Update(slot);
        await _uow.Repository<Appointment>().AddAsync(appointment, ct);

        // ── 6. Persist — UnitOfWork translates DbUpdateConcurrencyException
        //       to ConflictException ("first wins" booking race)
        await _uow.SaveChangesAsync(ct);

        // ── 7. Send confirmation email ─────────────────────────────────────
        var user = await _uow.Repository<User>()
            .GetByIdAsync(patient.UserId, ct);

        if (user is not null)
        {
            var emailBody =
                $"Your appointment with {provider.Name} is confirmed.\n" +
                $"Date & Time: {appointment.SlotTime:f} UTC\n" +
                $"Appointment ID: {appointment.Id}";

            await _emailSender.SendAsync(
                user.Email,
                "Appointment Confirmation",
                emailBody,
                ct);
        }

        return new BookingConfirmationDto(
            appointment.Id,
            provider.Id,
            provider.Name,
            appointment.SlotTime,
            appointment.Status.ToString(),
            conflictWarning);
    }
}
