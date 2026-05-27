using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Appointments;

/// <summary>
/// Loads a single appointment by ID with the navigations required by
/// <see cref="HealthPlatform.Infrastructure.Reminders.AppointmentReminderJob"/>:
/// Patient, Patient.User (for email address), and Provider (for name).
/// </summary>
public sealed class AppointmentForReminderSpecification : ISpecification<Appointment>
{
    private readonly Guid _appointmentId;

    public AppointmentForReminderSpecification(Guid appointmentId)
        => _appointmentId = appointmentId;

    public Expression<Func<Appointment, bool>>? Criteria =>
        a => a.Id == _appointmentId;

    public List<Expression<Func<Appointment, object>>> Includes =>
    [
        a => a.Patient,
        a => a.Patient.User,
        a => a.Provider,
    ];

    public Expression<Func<Appointment, object>>?      OrderBy           => null;
    public Expression<Func<Appointment, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
