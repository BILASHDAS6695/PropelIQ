using System.Linq.Expressions;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Entities;

namespace HealthPlatform.Application.Features.Providers;

/// <summary>
/// Returns all active providers, optionally filtered by specialty
/// (case-insensitive substring match). Orders by provider name ascending.
/// </summary>
internal sealed class ProvidersBySpecialtySpecification : ISpecification<Provider>
{
    private readonly string? _specialty;

    public ProvidersBySpecialtySpecification(string? specialty)
        => _specialty = specialty?.Trim().ToLowerInvariant();

    public Expression<Func<Provider, bool>>? Criteria =>
        string.IsNullOrEmpty(_specialty)
            ? null
            : p => p.Specialty != null
                && p.Specialty.ToLower().Contains(_specialty);

    public List<Expression<Func<Provider, object>>> Includes           => [];
    public Expression<Func<Provider, object>>?      OrderBy           => p => p.Name;
    public Expression<Func<Provider, object>>?      OrderByDescending => null;
    public bool IsPagingEnabled => false;
    public int  Skip            => 0;
    public int  Take            => 0;
}
