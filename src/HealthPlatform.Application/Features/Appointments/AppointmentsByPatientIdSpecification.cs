using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Returns all appointments for a given patient profile (by PatientProfile.Id),
/// ordered by SlotTime descending (most recent first).
/// Eagerly loads the Provider and Slot navigations.
/// </summary>
internal sealed class AppointmentsByPatientIdSpecification : ISpecification<Appointment>
{
    private readonly Guid _patientId;

    public AppointmentsByPatientIdSpecification(Guid patientId) => _patientId = patientId;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.PatientId == _patientId;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Provider,
        a => a.Slot!,
        a => a.IntakeRecord!,
    ];

    public Expression<Func<Appointment, object>>? OrderBy            => null;
    public Expression<Func<Appointment, object>>? OrderByDescending  => a => a.SlotTime;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
