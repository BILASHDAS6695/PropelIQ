using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Intake;

/// <summary>
/// Returns the most-recently-created IntakeRecord for an appointment (Take = 1).
/// </summary>
internal sealed class IntakeRecordByAppointmentSpecification : ISpecification<IntakeRecord>
{
    private readonly Guid _appointmentId;

    public IntakeRecordByAppointmentSpecification(Guid appointmentId)
        => _appointmentId = appointmentId;

    public Expression<Func<IntakeRecord, bool>>? Criteria
        => ir => ir.AppointmentId == _appointmentId;

    public List<Expression<Func<IntakeRecord, object>>> Includes           => [];
    public Expression<Func<IntakeRecord, object>>?      OrderBy           => null;
    public Expression<Func<IntakeRecord, object>>?      OrderByDescending => ir => ir.CreatedAt;
    public bool IsPagingEnabled => true;
    public int  Skip            => 0;
    public int  Take            => 1;
}
