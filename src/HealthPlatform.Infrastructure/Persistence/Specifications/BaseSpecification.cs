using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

/// <summary>
/// Base class for all specifications. Subclasses call the protected helpers
/// to compose criteria, includes, ordering, and paging.
/// </summary>
public abstract class BaseSpecification<T> : ISpecification<T>
{
    protected BaseSpecification() { }

    protected BaseSpecification(Expression<Func<T, bool>> criteria)
        => Criteria = criteria;

    public Expression<Func<T, bool>>?        Criteria          { get; }
    public List<Expression<Func<T, object>>> Includes          { get; } = [];
    public Expression<Func<T, object>>?      OrderBy           { get; private set; }
    public Expression<Func<T, object>>?      OrderByDescending { get; private set; }
    public bool                              IsPagingEnabled   { get; private set; }
    public int                               Skip              { get; private set; }
    public int                               Take              { get; private set; }

    protected void AddInclude(Expression<Func<T, object>> include)
        => Includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy)
        => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDesc)
        => OrderByDescending = orderByDesc;

    protected void ApplyPaging(int skip, int take)
    {
        Skip            = skip;
        Take            = take;
        IsPagingEnabled = true;
    }
}
