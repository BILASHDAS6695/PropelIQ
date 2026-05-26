# Task 003: AddSoftDelete Migration Generation, Apply, and Rollback Verification

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-009 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure / DevOps |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 and Task 002 complete and building cleanly |

## Objective

Generate a second EF Core migration that captures the three new soft-delete columns
(`is_deleted`, `deleted_at`, `deleted_by`) on every `AuditableEntity`-derived table,
then verify that:

1. The migration compiles and the generated SQL is correct.
2. `dotnet ef migrations remove` cleanly removes the migration (rollback tooling works).
3. The migration (re-generated after rollback check) can be applied to a clean database.

> **Note:** The `xmin` concurrency token added in Task 002 is a PostgreSQL system column —
> EF Core does **not** emit any DDL for it, so it does not appear in the migration diff.

## Acceptance Criteria Covered

- AC-7: Soft-delete filter configured globally — **migration captures schema columns**
- AC-8: Migration applies successfully to clean database
- AC-9: Rollback migration (`dotnet ef migrations remove`) works cleanly

---

## Implementation Steps

### 1. Confirm Build is Green

```bash
cd src
dotnet build HealthPlatform.sln
# Expected: Build succeeded. 0 Warning(s). 0 Error(s).
```

### 2. Generate the `AddSoftDelete` Migration

Run from `src/`:

```bash
dotnet ef migrations add AddSoftDelete \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api \
  --output-dir Persistence/Migrations
```

**Expected output:**

```
Build started...
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

Three new files appear in `src/HealthPlatform.Infrastructure/Persistence/Migrations/`:

| File | Description |
|---|---|
| `<timestamp>_AddSoftDelete.cs` | Up/Down migration methods |
| `<timestamp>_AddSoftDelete.Designer.cs` | EF model snapshot metadata |
| `ApplicationDbContextModelSnapshot.cs` | Updated (replaces previous snapshot) |

### 3. Verify Migration Content

Open the generated `AddSoftDelete.cs` and confirm the `Up` method contains
`AddColumn` calls for all six `AuditableEntity`-derived tables:

| Table | Expected columns added |
|---|---|
| `users` | `is_deleted` (bool, not null, default false), `deleted_at` (timestamptz, nullable), `deleted_by` (text, nullable) |
| `patient_profiles` | same three columns |
| `providers` | same three columns |
| `appointments` | same three columns |
| `intake_records` | same three columns |
| `clinical_documents` | same three columns |

The `Down` method must contain matching `DropColumn` calls for each.

Run a quick grep to confirm:

```powershell
Select-String -Path src\HealthPlatform.Infrastructure\Persistence\Migrations\*_AddSoftDelete.cs `
  -Pattern "is_deleted" | Measure-Object | Select-Object -ExpandProperty Count
# Expected: 12 or more matches (AddColumn + index per table × 6 tables)
```

### 4. Rollback Verification — `migrations remove`

Verify the EF tooling can cleanly undo the generated migration (before applying):

```bash
dotnet ef migrations remove \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

**Expected output:**

```
Build started...
Build succeeded.
Removing migration 'AddSoftDelete'.
Done.
```

Confirm the three migration files are deleted and `ApplicationDbContextModelSnapshot.cs`
is restored to its pre-`AddSoftDelete` state.

### 5. Re-generate the Migration

After the rollback check, re-generate the migration so it is ready to apply:

```bash
dotnet ef migrations add AddSoftDelete \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api \
  --output-dir Persistence/Migrations
```

### 6. Generate Idempotent SQL Script

Generate a SQL script that can be applied manually if `dotnet ef database update` is unavailable
(e.g., no local PostgreSQL credentials):

```bash
dotnet ef migrations script \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api \
  --idempotent \
  --output ..\infra\postgres\migrations.sql
```

This **overwrites** the existing `infra/postgres/migrations.sql` with the full cumulative script
(both `InitialCreate` and `AddSoftDelete`).

Verify line count increased:

```powershell
Get-Content C:\Propel\PropelIQ\infra\postgres\migrations.sql | Measure-Object -Line
# Expected: more than 388 lines (previous script was 388)
```

### 7. Apply Migration (Requires Live Database)

If a PostgreSQL instance is available:

```bash
dotnet ef database update \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

Or apply the idempotent SQL manually via psql or pgAdmin:

```bash
psql -U postgres -d healthplatform -f infra/postgres/migrations.sql
```

### 8. Final Build Check

```bash
dotnet build HealthPlatform.sln
# Expected: Build succeeded. 0 Warning(s). 0 Error(s).
```

---

## Expected Final File Tree

```
src/HealthPlatform.Infrastructure/Persistence/Migrations/
├── 20260526100151_InitialCreate.cs
├── 20260526100151_InitialCreate.Designer.cs
├── <timestamp>_AddSoftDelete.cs          ← new
├── <timestamp>_AddSoftDelete.Designer.cs ← new
└── ApplicationDbContextModelSnapshot.cs  ← updated

infra/postgres/
└── migrations.sql                        ← updated (cumulative, idempotent)
```

---

## Verification Checklist

- [ ] `dotnet ef migrations add AddSoftDelete` executes without errors
- [ ] Migration `Up` adds `is_deleted`, `deleted_at`, `deleted_by` to all 6 AuditableEntity tables
- [ ] Migration `Down` has matching `DropColumn` calls
- [ ] `dotnet ef migrations remove` removes the migration cleanly
- [ ] Migration re-generated after rollback check
- [ ] `infra/postgres/migrations.sql` regenerated (cumulative script, > 388 lines)
- [ ] `dotnet build` passes — 0 errors, 0 warnings
- [ ] Migration applied to clean database (or SQL script ready for manual apply)
