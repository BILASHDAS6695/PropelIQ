using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class GetMyAppointmentsQueryHandler
    : IRequestHandler<GetMyAppointmentsQuery, IReadOnlyList<PatientAppointmentDto>>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public GetMyAppointmentsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<PatientAppointmentDto>> Handle(
        GetMyAppointmentsQuery query,
        CancellationToken      ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated.");

        var profiles = await _uow.Repository<PatientProfile>()
            .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

        if (profiles.Count == 0)
            throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

        var patientId = profiles[0].Id;
        var patientName = $"{profiles[0].FirstName} {profiles[0].LastName}";

        var appointments = await _uow.Repository<Appointment>()
            .GetAsync(new AppointmentsByPatientIdSpecification(patientId), ct);

        return appointments
            .Select(a => new PatientAppointmentDto(
                AppointmentId: a.Id,
                ProviderId:    a.ProviderId,
                ProviderName:  a.Provider.Name,
                SlotTime:      a.SlotTime,
                EndTime:       a.Slot?.EndTime ?? a.SlotTime.AddMinutes(30),
                Status:        a.Status.ToString(),
                VisitReason:   a.VisitReason,
                PatientName:   patientName))
            .ToList()
            .AsReadOnly();
    }
}
