using HealthPlatform.Application;
using HealthPlatform.Application.Features.Appointments;
using HealthPlatform.Application.Interfaces;
using HealthPlatform.Domain.Common.Exceptions;
using HealthPlatform.Domain.Entities;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Tests.Application;

public class RegisterWalkInCommandTests
{
    // ── helpers ───────────────────────────────────────────────────────────────

    private static ISender BuildSender(IUnitOfWork uow)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped(_ => uow);
        return services.BuildServiceProvider().GetRequiredService<ISender>();
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterWalkIn_ProviderNotFound_ThrowsNotFoundException()
    {
        // Arrange — no provider in repo
        var sender = BuildSender(new WalkInStubUnitOfWork(null, null));

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sender.Send(new RegisterWalkInCommand(Guid.NewGuid(), Guid.NewGuid())));
    }

    [Fact]
    public async Task RegisterWalkIn_ValidPatientAndProvider_ReturnsQueuePosition1()
    {
        // Arrange — provider + patient exist, no existing walk-ins today
        var provider = new Provider { Id = Guid.NewGuid(), Name = "Dr. Smith" };
        var patient  = new PatientProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        var sender = BuildSender(new WalkInStubUnitOfWork(provider, patient));

        // Act
        var result = await sender.Send(
            new RegisterWalkInCommand(patient.Id, provider.Id, "Headache"));

        // Assert
        Assert.Equal(1,             result.QueuePosition);
        Assert.Equal(provider.Id,   result.ProviderId);
        Assert.Equal(provider.Name, result.ProviderName);
        Assert.Equal("WalkIn",      result.Status);
    }
}

// ── Stubs ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Stub UnitOfWork for walk-in tests. Serves a single Provider (GetByIdAsync)
/// and a single PatientProfile (GetAsync). Appointment repository returns empty
/// for queue-position queries and accepts AddAsync silently.
/// </summary>
internal sealed class WalkInStubUnitOfWork : IUnitOfWork
{
    private readonly Provider?       _provider;
    private readonly PatientProfile? _patient;

    public WalkInStubUnitOfWork(Provider? provider, PatientProfile? patient)
    {
        _provider = provider;
        _patient  = patient;
    }

    public IRepository<T> Repository<T>() where T : class
    {
        if (typeof(T) == typeof(Provider))
            return (IRepository<T>)(object)new SingleEntityRepository<Provider>(_provider);

        if (typeof(T) == typeof(PatientProfile))
            return (IRepository<T>)(object)new SingleEntityRepository<PatientProfile>(_patient);

        // Appointment repository: empty GetAsync (no existing walk-ins), silent AddAsync.
        return new EmptyRepository<T>();
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    public void Dispose() { }
}

/// <summary>
/// Generic single-entity stub — returns the given entity for GetByIdAsync
/// and wraps it in a list for GetAsync (spec not applied; sufficient for handler unit tests).
/// </summary>
internal sealed class SingleEntityRepository<T> : IRepository<T> where T : class
{
    private readonly T? _entity;

    public SingleEntityRepository(T? entity) => _entity = entity;

    public Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_entity);

    public Task<IReadOnlyList<T>> GetAsync(
        ISpecification<T> spec, CancellationToken ct = default)
    {
        IReadOnlyList<T> result = _entity is null
            ? Array.Empty<T>()
            : [_entity];
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<T> result = _entity is null ? Array.Empty<T>() : [_entity];
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(ISpecification<T> spec, CancellationToken ct = default)
        => Task.FromResult(_entity is null ? 0 : 1);

    public Task AddAsync(T entity, CancellationToken ct = default) => Task.CompletedTask;
    public void Update(T entity) { }
    public void Delete(T entity) { }
}
