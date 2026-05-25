# Task 001: Solution Structure & Project Configuration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-001 |
| **Epic** | EP-TECH |
| **Layer** | Infrastructure / Build |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | None (first task) |

## Objective

Create the .NET 8 solution file with four class library projects following Clean Architecture dependency rules, configured with strict build settings.

## Implementation Steps

### 1. Create Solution File

```bash
dotnet new sln -n HealthPlatform -o src
```

### 2. Create Projects

```bash
cd src
dotnet new webapi -n HealthPlatform.Api --framework net8.0
dotnet new classlib -n HealthPlatform.Application --framework net8.0
dotnet new classlib -n HealthPlatform.Domain --framework net8.0
dotnet new classlib -n HealthPlatform.Infrastructure --framework net8.0
```

### 3. Add Projects to Solution

```bash
dotnet sln HealthPlatform.sln add HealthPlatform.Api/HealthPlatform.Api.csproj
dotnet sln HealthPlatform.sln add HealthPlatform.Application/HealthPlatform.Application.csproj
dotnet sln HealthPlatform.sln add HealthPlatform.Domain/HealthPlatform.Domain.csproj
dotnet sln HealthPlatform.sln add HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj
```

### 4. Configure Project References (Clean Architecture Rules)

Dependency flow: Api → Application → Domain; Api → Infrastructure → Application → Domain

```bash
# Domain: NO project references (innermost layer)

# Application references Domain only
dotnet add HealthPlatform.Application/HealthPlatform.Application.csproj reference HealthPlatform.Domain/HealthPlatform.Domain.csproj

# Infrastructure references Application (and transitively Domain)
dotnet add HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj reference HealthPlatform.Application/HealthPlatform.Application.csproj

# Api references Application and Infrastructure (composition root)
dotnet add HealthPlatform.Api/HealthPlatform.Api.csproj reference HealthPlatform.Application/HealthPlatform.Application.csproj
dotnet add HealthPlatform.Api/HealthPlatform.Api.csproj reference HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj
```

### 5. Configure Build Properties

Apply to all `.csproj` files:

```xml
<PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

Alternatively, create a `Directory.Build.props` at solution root:

```xml
<Project>
    <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
</Project>
```

## Acceptance Criteria

- [ ] `HealthPlatform.sln` exists with four projects listed
- [ ] Domain project has zero `<ProjectReference>` elements
- [ ] Application references only Domain
- [ ] Infrastructure references only Application
- [ ] Api references Application and Infrastructure
- [ ] All projects target `net8.0`
- [ ] `<Nullable>enable</Nullable>` set on all projects
- [ ] `<ImplicitUsings>enable</ImplicitUsings>` set on all projects
- [ ] `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` set on all projects
- [ ] `dotnet build` succeeds with zero warnings

## Verification

```bash
dotnet build HealthPlatform.sln --configuration Release
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-008 | .NET 8 targeting |
| TR-009 | 4-layer Clean Architecture |
| ADR-001 | Modular Monolith foundation |
