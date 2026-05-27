using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

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

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                "A concurrency conflict occurred. The resource was modified by another request.")
            { Data = { ["inner"] = ex.Message } };
        }
    }

    public void Dispose() => _context.Dispose();
}
