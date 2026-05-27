using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Loads a single appointment by primary key, eagerly including
/// its optional Slot and its owning Patient.  Used by the
/// cancel and reschedule handlers to avoid separate round-trips.
/// </summary>
internal sealed class AppointmentByIdWithSlotAndPatientSpecification
    : ISpecification<Appointment>
{
    private readonly Guid _appointmentId;

    public AppointmentByIdWithSlotAndPatientSpecification(Guid appointmentId)
        => _appointmentId = appointmentId;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.Id == _appointmentId;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Slot!,
        a => a.Patient,
        a => a.Patient.User,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
