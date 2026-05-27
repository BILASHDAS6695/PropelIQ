using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Matches <see cref="ProviderScheduleRule"/> records for a given provider
/// and day-of-week. Used to detect duplicates at creation time.
/// </summary>
internal sealed class ScheduleRuleByProviderAndDaySpecification
    : ISpecification<ProviderScheduleRule>
{
    private readonly Guid      _providerId;
    private readonly DayOfWeek _dayOfWeek;

    public ScheduleRuleByProviderAndDaySpecification(Guid providerId, DayOfWeek dayOfWeek)
    {
        _providerId = providerId;
        _dayOfWeek  = dayOfWeek;
    }

    public Expression<Func<ProviderScheduleRule, bool>>? Criteria =>
        r => r.ProviderId == _providerId && r.DayOfWeek == _dayOfWeek;

    public List<Expression<Func<ProviderScheduleRule, object>>> Includes => [];
    public Expression<Func<ProviderScheduleRule, object>>?      OrderBy           => null;
    public Expression<Func<ProviderScheduleRule, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
