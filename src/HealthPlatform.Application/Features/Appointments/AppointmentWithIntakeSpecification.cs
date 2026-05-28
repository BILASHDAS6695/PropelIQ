using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Fetches a single appointment by ID, eagerly loading IntakeRecord, Patient, and Slot
/// for use by intake window evaluation and walk-in intake triggering.
/// </summary>
internal sealed class AppointmentWithIntakeSpecification : ISpecification<Appointment>
{
    public AppointmentWithIntakeSpecification(Guid appointmentId)
    {
        Criteria = a => a.Id == appointmentId;
        IsPagingEnabled = true;
        Skip = 0;
        Take = 1;
    }

    public Expression<Func<Appointment, bool>>? Criteria { get; }

    public List<Expression<Func<Appointment, object>>> Includes { get; } =
    [
        a => a.IntakeRecord!,
        a => a.Patient,
        a => a.Slot!,
    ];

    public Expression<Func<Appointment, object>>? OrderBy            => null;
    public Expression<Func<Appointment, object>>? OrderByDescending  => null;
    public int  Skip            { get; }
    public int  Take            { get; }
    public bool IsPagingEnabled { get; }
}
