# Task 005: API Layer DI Wiring & Unit Test Project

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-001 |
| **Epic** | EP-TECH |
| **Layer** | Api / Test |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001, Task 003, Task 004 |

## Objective

Wire up all layer registrations in the API's `Program.cs` via dependency injection, and create the xUnit test project to verify the full solution builds and the MediatR pipeline functions correctly.

## Implementation Steps

### 1. Configure Program.cs

**File:** `HealthPlatform.Api/Program.cs`

```csharp
using HealthPlatform.Application;
using HealthPlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Layer registrations
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
```

### 2. Add Connection String Placeholder

**File:** `HealthPlatform.Api/appsettings.json`

Add to the existing JSON:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=healthplatform;Username=postgres;Password=postgres"
  }
}
```

### 3. Create Unit Test Project

```bash
dotnet new xunit -n HealthPlatform.Tests --framework net8.0
dotnet sln HealthPlatform.sln add HealthPlatform.Tests/HealthPlatform.Tests.csproj
dotnet add HealthPlatform.Tests/HealthPlatform.Tests.csproj reference HealthPlatform.Application/HealthPlatform.Application.csproj
dotnet add HealthPlatform.Tests/HealthPlatform.Tests.csproj reference HealthPlatform.Domain/HealthPlatform.Domain.csproj
```

### 4. Add Test Packages

```bash
cd HealthPlatform.Tests
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package MediatR
```

### 5. Create Verification Test

**File:** `HealthPlatform.Tests/Application/PingQueryTests.cs`

```csharp
using HealthPlatform.Application.Features.Sample;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using HealthPlatform.Application;

namespace HealthPlatform.Tests.Application;

public class PingQueryTests
{
    [Fact]
    public async Task PingQuery_ReturnsPong()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        // Act
        var result = await mediator.Send(new PingQuery());

        // Assert
        Assert.Equal("Pong", result);
    }
}
```

### 6. Create Domain Base Entity Test

**File:** `HealthPlatform.Tests/Domain/BaseEntityTests.cs`

```csharp
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Tests.Domain;

public class BaseEntityTests
{
    private sealed class TestEntity : AuditableEntity { }

    [Fact]
    public void AuditableEntity_InheritsBaseEntity_HasIdProperty()
    {
        // Arrange & Act
        var entity = new TestEntity { Id = Guid.NewGuid() };

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.IsAssignableFrom<BaseEntity>(entity);
    }

    [Fact]
    public void AuditableEntity_HasAuditProperties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var entity = new TestEntity
        {
            CreatedAt = now,
            UpdatedAt = now
        };

        // Assert
        Assert.Equal(now, entity.CreatedAt);
        Assert.Equal(now, entity.UpdatedAt);
    }
}
```

### 7. Remove Default UnitTest1.cs

Delete auto-generated `UnitTest1.cs`.

## Acceptance Criteria

- [ ] `Program.cs` calls `AddApplication()` and `AddInfrastructure(configuration)`
- [ ] `appsettings.json` contains `ConnectionStrings:DefaultConnection`
- [ ] `HealthPlatform.Tests` project created with xUnit
- [ ] Test project references Application and Domain
- [ ] `PingQueryTests.PingQuery_ReturnsPong` passes — validates MediatR pipeline
- [ ] `BaseEntityTests` validates entity hierarchy
- [ ] Full solution builds: `dotnet build HealthPlatform.sln`
- [ ] All tests pass: `dotnet test HealthPlatform.sln`

## Verification

```bash
dotnet build HealthPlatform.sln --configuration Release
dotnet test HealthPlatform.sln --no-build --configuration Release
```

Expected output: Build succeeded, all tests pass (green).

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-001 AC-6 | Api configures DI for all layers |
| US-001 AC-8 | Solution builds successfully |
| US-001 AC-9 | xUnit test project created |
| TR-010 | MediatR pipeline verified via test |
