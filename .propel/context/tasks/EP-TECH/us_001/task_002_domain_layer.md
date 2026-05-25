# Task 002: Domain Layer — Base Entity Classes

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-001 |
| **Epic** | EP-TECH |
| **Layer** | Domain |
| **Priority** | Critical |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 |

## Objective

Implement the base entity classes in the Domain project that all future domain entities will inherit from, providing consistent identity and audit trail properties.

## Implementation Steps

### 1. Create Base Entity

**File:** `HealthPlatform.Domain/Common/BaseEntity.cs`

```csharp
namespace HealthPlatform.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; }
}
```

### 2. Create Auditable Entity

**File:** `HealthPlatform.Domain/Common/AuditableEntity.cs`

```csharp
namespace HealthPlatform.Domain.Common;

public abstract class AuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
```

### 3. Remove Default Class1.cs

Delete the auto-generated `Class1.cs` from the Domain project.

## Acceptance Criteria

- [ ] `BaseEntity` abstract class exists with `Guid Id` property
- [ ] `AuditableEntity` extends `BaseEntity` with `CreatedAt` and `UpdatedAt` (`DateTimeOffset`)
- [ ] Namespace follows pattern `HealthPlatform.Domain.Common`
- [ ] No default/generated files remain (e.g., `Class1.cs`)
- [ ] Domain project builds with zero warnings

## Verification

```bash
dotnet build HealthPlatform.Domain/HealthPlatform.Domain.csproj
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-009 | Clean Architecture — Domain is innermost |
| US-001 AC-3 | Base entity classes with Id, CreatedAt, UpdatedAt |
