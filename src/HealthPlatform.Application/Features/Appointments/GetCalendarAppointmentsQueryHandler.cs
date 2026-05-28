using HealthPlatform.Application.Features.Auth;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using MediatR;

namespace HealthPlatform.Application.Features.Appointments;

internal sealed class GetCalendarAppointmentsQueryHandler
    : IRequestHandler<GetCalendarAppointmentsQuery, IReadOnlyList<CalendarAppointmentDto>>
{
    private readonly IUnitOfWork         _uow;
    private readonly ICurrentUserService _currentUser;

    public GetCalendarAppointmentsQueryHandler(IUnitOfWork uow, ICurrentUserService currentUser)
    {
        _uow         = uow;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CalendarAppointmentDto>> Handle(
        GetCalendarAppointmentsQuery query,
        CancellationToken            ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
            throw new UnauthorizedAccessException("User must be authenticated.");

        var callerUsers = await _uow.Repository<User>()
            .GetAsync(new UserByIdSpecification(_currentUser.UserId.Value), ct);

        var isStaffOrAdmin = callerUsers.Count > 0
            && callerUsers[0].Role is UserRole.Staff or UserRole.Admin;

        IReadOnlyList<Appointment> appointments;

        if (isStaffOrAdmin)
        {
            // Staff path: return all appointments for the given provider (or all if null)
            appointments = await _uow.Repository<Appointment>()
                .GetAsync(
                    new ProviderAppointmentsInDateRangeSpecification(
                        query.ProviderId, query.From, query.To),
                    ct);
        }
        else
        {
            // Patient path: resolve patient profile and return own appointments only
            var profiles = await _uow.Repository<PatientProfile>()
                .GetAsync(new PatientProfileByUserIdSpecification(_currentUser.UserId.Value), ct);

            if (profiles.Count == 0)
                throw new NotFoundException(nameof(PatientProfile), _currentUser.UserId.Value);

            appointments = await _uow.Repository<Appointment>()
                .GetAsync(
                    new AppointmentsInDateRangeSpecification(profiles[0].Id, query.From, query.To),
                    ct);
        }

        return appointments
            .Select(a => new CalendarAppointmentDto(
                AppointmentId: a.Id,
                ProviderId:    a.ProviderId,
                ProviderName:  a.Provider.Name,
                PatientName:   $"{a.Patient.FirstName} {a.Patient.LastName}",
                SlotTime:      a.SlotTime,
                EndTime:       a.Slot?.EndTime ?? a.SlotTime.AddMinutes(30),
                Status:        a.Status.ToString(),
                VisitReason:   a.VisitReason))
            .ToList()
            .AsReadOnly();
    }
}
