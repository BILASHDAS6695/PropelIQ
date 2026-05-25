# Task 003: Application Layer — MediatR, FluentValidation & Pipeline Behaviors

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-001 |
| **Epic** | EP-TECH |
| **Layer** | Application |
| **Priority** | Critical |
| **Estimated Effort** | 3 hours |
| **Dependencies** | Task 001, Task 002 |

## Objective

Configure the Application layer with MediatR for CQRS pattern, FluentValidation for input validation, and register pipeline behaviors for cross-cutting concerns (validation, logging).

## Implementation Steps

### 1. Add NuGet Packages

```bash
cd HealthPlatform.Application
dotnet add package MediatR --version 12.*
dotnet add package FluentValidation --version 11.*
dotnet add package FluentValidation.DependencyInjectionExtensions --version 11.*
dotnet add package Microsoft.Extensions.Logging.Abstractions
```

### 2. Create Validation Pipeline Behavior

**File:** `HealthPlatform.Application/Behaviors/ValidationBehavior.cs`

```csharp
using FluentValidation;
using MediatR;

namespace HealthPlatform.Application.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);

        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
            throw new ValidationException(failures);

        return await next();
    }
}
```

### 3. Create Logging Pipeline Behavior

**File:** `HealthPlatform.Application/Behaviors/LoggingBehavior.cs`

```csharp
using MediatR;
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Application.Behaviors;

public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName}", requestName);

        var response = await next();

        _logger.LogInformation("Handled {RequestName}", requestName);

        return response;
    }
}
```

### 4. Create DI Registration Extension

**File:** `HealthPlatform.Application/DependencyInjection.cs`

```csharp
using FluentValidation;
using HealthPlatform.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HealthPlatform.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
```

### 5. Create Sample Command (Ping/Pong)

**File:** `HealthPlatform.Application/Features/Sample/PingQuery.cs`

```csharp
using MediatR;

namespace HealthPlatform.Application.Features.Sample;

public sealed record PingQuery : IRequest<string>;

public sealed class PingQueryHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult("Pong");
    }
}
```

### 6. Remove Default Class1.cs

Delete auto-generated `Class1.cs`.

## Acceptance Criteria

- [ ] MediatR 12.x package installed
- [ ] FluentValidation 11.x package installed
- [ ] `ValidationBehavior<TRequest, TResponse>` implements `IPipelineBehavior`
- [ ] `LoggingBehavior<TRequest, TResponse>` implements `IPipelineBehavior`
- [ ] `DependencyInjection.AddApplication()` registers MediatR with both behaviors
- [ ] Validators auto-registered from assembly
- [ ] Sample `PingQuery`/`PingQueryHandler` demonstrates command pipeline
- [ ] Application project builds with zero warnings

## Verification

```bash
dotnet build HealthPlatform.Application/HealthPlatform.Application.csproj
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-010 | MediatR CQRS pattern |
| TR-014 | FluentValidation pipeline |
| US-001 AC-4 | MediatR with sample command/query |
| US-001 AC-7 | FluentValidation pipeline behavior |
