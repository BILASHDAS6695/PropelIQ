# Task 001: Repository Pattern — Interfaces and Implementations

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-012 |
| **Epic** | EP-DATA |
| **Layer** | Application (interfaces) + Infrastructure (implementations) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | US-008 (EF Core + ApplicationDbContext must exist) |

## Objective

Introduce the repository, Unit of Work, and specification abstractions required
by Clean Architecture. No application code should reference `DbContext` or
`IQueryable` directly — all data access flows through `IRepository<T>` and
`IUnitOfWork`. `ISpecification<T>` encapsulates query criteria and includes so
that complex queries remain composable and testable without leaking EF concerns.

## Acceptance Criteria Covered

- AC-4: `IRepository<T>` with `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Delete`, `GetAsync(spec)`, `CountAsync(spec)`
- AC-5: `IUnitOfWork` wraps `ApplicationDbContext.SaveChangesAsync` with audit integration already provided by the existing `AuditableEntity` interceptors
- AC-6: Repository implementations use EF Core `DbSet<T>` operations
- AC-7: Specification pattern via `ISpecification<T>` / `BaseSpecification<T>` / `SpecificationEvaluator<T>`

## Implementation Steps

### 1. Create `ISpecification<T>` in Application Layer

Create `src/HealthPlatform.Application/Interfaces/ISpecification.cs`:

```csharp
using System.Linq.Expressions;

namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Encapsulates query criteria, eager-loading includes, ordering, and paging
/// so that callers never construct raw LINQ outside the Infrastructure layer.
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>?         Criteria           { get; }
    List<Expression<Func<T, object>>>  Includes           { get; }
    Expression<Func<T, object>>?       OrderBy            { get; }
    Expression<Func<T, object>>?       OrderByDescending  { get; }
    bool                               IsPagingEnabled    { get; }
    int                                Skip               { get; }
    int                                Take               { get; }
}
```

### 2. Create `IRepository<T>` in Application Layer

Create `src/HealthPlatform.Application/Interfaces/IRepository.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Generic data-access contract. Does NOT expose IQueryable — all complex
/// queries are expressed through <see cref="ISpecification{T}"/>.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}
```

### 3. Create `IUnitOfWork` in Application Layer

Create `src/HealthPlatform.Application/Interfaces/IUnitOfWork.cs`:

```csharp
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
```

### 4. Create `BaseSpecification<T>` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Persistence/Specifications/BaseSpecification.cs`:

```csharp
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
```

### 5. Create `SpecificationEvaluator<T>` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Persistence/Specifications/SpecificationEvaluator.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace HealthPlatform.Infrastructure.Persistence.Specifications;

/// <summary>
/// Applies an <see cref="ISpecification{T}"/> to an <see cref="IQueryable{T}"/>
/// so that <see cref="GenericRepository{T}"/> stays free of query-building logic.
/// </summary>
internal static class SpecificationEvaluator<T> where T : class
{
    public static IQueryable<T> GetQuery(IQueryable<T> query, ISpecification<T> spec)
    {
        if (spec.Criteria is not null)
            query = query.Where(spec.Criteria);

        query = spec.Includes
            .Aggregate(query, (q, include) => q.Include(include));

        if (spec.OrderBy is not null)
            query = query.OrderBy(spec.OrderBy);
        else if (spec.OrderByDescending is not null)
            query = query.OrderByDescending(spec.OrderByDescending);

        if (spec.IsPagingEnabled)
            query = query.Skip(spec.Skip).Take(spec.Take);

        return query;
    }
}
```

### 6. Create `GenericRepository<T>` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Persistence/GenericRepository.cs`:

```csharp
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
```

### 7. Create `UnitOfWork` in Infrastructure Layer

Create `src/HealthPlatform.Infrastructure/Persistence/UnitOfWork.cs`:

```csharp
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
```

### 8. Register `IUnitOfWork` in `DependencyInjection.cs`

Add after the `ICacheService` registration:

```csharp
services.AddScoped<IUnitOfWork, UnitOfWork>();
```

`IRepository<T>` is accessed through `IUnitOfWork.Repository<T>()`, so no
separate registration is required.

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Application/Interfaces/ISpecification.cs` | New — specification interface |
| `src/HealthPlatform.Application/Interfaces/IRepository.cs` | New — generic repository interface |
| `src/HealthPlatform.Application/Interfaces/IUnitOfWork.cs` | New — unit of work interface |
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/BaseSpecification.cs` | New — abstract specification base |
| `src/HealthPlatform.Infrastructure/Persistence/Specifications/SpecificationEvaluator.cs` | New — spec-to-IQueryable translator |
| `src/HealthPlatform.Infrastructure/Persistence/GenericRepository.cs` | New — EF Core repository implementation |
| `src/HealthPlatform.Infrastructure/Persistence/UnitOfWork.cs` | New — unit of work implementation |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Register `IUnitOfWork` → `UnitOfWork` |

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

## Notes

- `GenericRepository<T>` is `internal sealed` — callers always go through
  `IUnitOfWork.Repository<T>()` which enforces the Clean Architecture boundary.
- `AsNoTracking()` on read queries avoids unnecessary change-tracking overhead;
  `FindAsync` (used by `GetByIdAsync`) uses the identity map for cached lookups.
- `SpecificationEvaluator<T>` is `internal static` — it is an implementation
  detail of `GenericRepository<T>` and must not leak to callers.
- Audit stamping (`CreatedAt`, `UpdatedAt`) continues to be handled by the
  existing `UpdateAuditableEntities()` interceptor in `ApplicationDbContext`,
  so `UnitOfWork.SaveChangesAsync` gets it for free.
- `IUnitOfWork.Dispose()` delegates to `DbContext.Dispose()`; DI (Scoped) will
  call it automatically at the end of the HTTP request.
