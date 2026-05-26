namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Coordinates multiple repository operations within a single transaction
/// boundary. Audit fields (CreatedAt, UpdatedAt) are stamped automatically
/// by <see cref="HealthPlatform.Infrastructure.Persistence.ApplicationDbContext"/>.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<T> Repository<T>() where T : class;
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
