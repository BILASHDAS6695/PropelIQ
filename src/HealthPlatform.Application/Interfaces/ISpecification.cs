using System.Linq.Expressions;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Encapsulates query criteria, eager-loading includes, ordering, and paging
/// so that callers never construct raw LINQ outside the Infrastructure layer.
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>?        Criteria          { get; }
    List<Expression<Func<T, object>>> Includes          { get; }
    Expression<Func<T, object>>?      OrderBy           { get; }
    Expression<Func<T, object>>?      OrderByDescending { get; }
    bool                              IsPagingEnabled   { get; }
    int                               Skip              { get; }
    int                               Take              { get; }
}
