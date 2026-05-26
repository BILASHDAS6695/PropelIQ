using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.TryGetValue(type, out var repo))
        {
            repo = new GenericRepository<T>(_context);
            _repositories[type] = repo;
        }
        return (IRepository<T>)repo;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _context.SaveChangesAsync(ct);

    public void Dispose() => _context.Dispose();
}
