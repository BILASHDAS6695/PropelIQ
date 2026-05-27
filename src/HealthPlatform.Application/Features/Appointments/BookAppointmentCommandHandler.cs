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

        // ── 3. Duplicate-booking guard ─────────────────────────────────────
        var slotDate  = DateOnly.FromDateTime(slot.StartTime.UtcDateTime);
        var duplicates = await _uow.Repository<Appointment>()
            .GetAsync(
                new ActiveAppointmentByPatientProviderDateSpecification(
                    patient.Id, slot.ProviderId, slotDate), ct);

        if (duplicates.Count > 0)
            throw new ConflictException(
                "You already have an active appointment with this provider on the requested date.");

        // ── 4. Load provider for confirmation DTO ──────────────────────────
        var provider = await _uow.Repository<Provider>()
            .GetByIdAsync(slot.ProviderId, ct)
            ?? throw new NotFoundException(nameof(Provider), slot.ProviderId);

        // ── 5. Create appointment + mark slot Booked ───────────────────────
        var appointment = new Appointment
        {
            Id          = Guid.NewGuid(),
            PatientId   = patient.Id,
            ProviderId  = slot.ProviderId,
            SlotId      = slot.Id,
            SlotTime    = slot.StartTime,
            Status      = AppointmentStatus.Scheduled,
            VisitReason = command.VisitReason,
            IsWalkIn    = false
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
            appointment.Status.ToString());
    }
}
