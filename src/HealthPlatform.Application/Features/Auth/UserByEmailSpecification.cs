using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Auth;

/// <summary>
/// Matches a single <see cref="User"/> by email address (case-insensitive).
/// Used by <see cref="RegisterPatientCommandHandler"/> to detect duplicate registrations.
/// </summary>
internal sealed class UserByEmailSpecification : ISpecification<User>
{
    private readonly string _email;

    public UserByEmailSpecification(string email) =>
        _email = email.ToLowerInvariant();

    public Expression<Func<User, bool>>? Criteria =>
        u => u.Email.ToLower() == _email;

    public List<Expression<Func<User, object>>> Includes => [];
    public Expression<Func<User, object>>? OrderBy => null;
    public Expression<Func<User, object>>? OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int Skip => 0;
    public int Take => 0;
}
