using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>Loads a User by its primary key (no navigation loading).</summary>
internal sealed class UserByIdSpecification : ISpecification<User>
{
    private readonly Guid _id;
    public UserByIdSpecification(Guid id) => _id = id;

    public Expression<Func<User, bool>>? Criteria => u => u.Id == _id;
    public List<Expression<Func<User, object>>> Includes => [];
    public Expression<Func<User, object>>? OrderBy            => null;
    public Expression<Func<User, object>>? OrderByDescending  => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
