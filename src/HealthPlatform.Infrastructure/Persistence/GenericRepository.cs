using HealthPlatform.Application.Interfaces;
using HealthPlatform.Infrastructure.Persistence.Specifications;
using Microsoft.EntityFrameworkCore;

namespace HealthPlatform.Infrastructure.Persistence;

internal sealed class GenericRepository<T> : IRepository<T> where T : class
{
    private readonly ApplicationDbContext _context;

    public GenericRepository(ApplicationDbContext context) => _context = context;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Set<T>().FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _context.Set<T>().AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> GetAsync(ISpecification<T> spec,
                                                  CancellationToken ct = default)
        => await SpecificationEvaluator<T>
            .GetQuery(_context.Set<T>().AsNoTracking(), spec)
            .ToListAsync(ct);

    public async Task<int> CountAsync(ISpecification<T> spec,
                                       CancellationToken ct = default)
        => await SpecificationEvaluator<T>
            .GetQuery(_context.Set<T>().AsNoTracking(), spec)
            .CountAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default)
        => await _context.Set<T>().AddAsync(entity, ct);

    public void Update(T entity) => _context.Set<T>().Update(entity);

    public void Delete(T entity) => _context.Set<T>().Remove(entity);
}
