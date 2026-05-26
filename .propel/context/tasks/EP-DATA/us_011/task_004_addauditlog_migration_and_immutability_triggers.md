# Task 004: AddAuditLog Migration, PostgreSQL Immutability Triggers, and SQL Script Update

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-011 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure / Database |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001, Task 002, Task 003 all complete; `dotnet build` passing |

## Objective

1. Generate the `AddAuditLog` EF Core migration that creates the `audit_logs` table with all
   required columns and indexes.
2. Extend the migration's `Up()` method with PostgreSQL `RULE` statements that prevent `UPDATE`
   and `DELETE` operations at the database level (second line of defence alongside the application
   interceptor).
3. Add a corresponding rollback for those rules in `Down()`.
4. Regenerate the idempotent cumulative SQL script at `infra/postgres/migrations.sql`.

## Acceptance Criteria Covered

- AC-1: AuditLog table created with all required columns (DDL applied)
- AC-2: Database trigger/rule prevents UPDATE operations on AuditLog table
- AC-3: Database trigger/rule prevents DELETE operations on AuditLog table
- AC-9: 7-year retention policy documented in the migration comment; no automatic purging configured

---

## Implementation Steps

### 1. Stop the Running API (if active)

The running API locks DLLs required for migration generation.

```powershell
# Find and stop the dotnet process running the API
Get-Process -Name "dotnet" | Where-Object { $_.MainWindowTitle -eq "" } | Stop-Process -Force
```

Or use the PID from Task Manager.

### 2. Generate the EF Core Migration

Run from `src/`:

```bash
dotnet ef migrations add AddAuditLog \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

**Expected output:**

```
Build succeeded.
Done. To undo this action, use 'ef migrations remove'
```

A new file will be created:
`src/HealthPlatform.Infrastructure/Persistence/Migrations/<timestamp>_AddAuditLog.cs`

### 3. Add PostgreSQL Immutability Rules to the Migration

Open the generated migration file and extend `Up()` and `Down()`:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // --- EF Core generated table/index DDL is here (do not modify) ---

    // PostgreSQL RULE: block UPDATE on audit_logs (append-only enforcement)
    // ADR-006 — second line of defence; application interceptor is the first.
    // Retention: 7 years per NFR-015; no automated purge is configured.
    migrationBuilder.Sql(@"
        CREATE OR REPLACE RULE audit_logs_no_update AS
            ON UPDATE TO audit_logs
            DO INSTEAD NOTHING;
    ");

    // PostgreSQL RULE: block DELETE on audit_logs
    migrationBuilder.Sql(@"
        CREATE OR REPLACE RULE audit_logs_no_delete AS
            ON DELETE TO audit_logs
            DO INSTEAD NOTHING;
    ");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // Drop immutability rules before dropping the table
    migrationBuilder.Sql("DROP RULE IF EXISTS audit_logs_no_update ON audit_logs;");
    migrationBuilder.Sql("DROP RULE IF EXISTS audit_logs_no_delete ON audit_logs;");

    // --- EF Core generated table/index drop DDL is here (do not modify) ---
}
```

> **Why RULE instead of TRIGGER?**  
> `CREATE RULE … DO INSTEAD NOTHING` silently discards the disallowed command and returns
> success — it does not raise an exception that would abort the caller's transaction.
> This matches the technical note in US-011. If a hard error is preferred in future,
> replace with a `BEFORE UPDATE/DELETE` trigger that raises `EXCEPTION`.

### 4. Verify Migration Rollback

```bash
dotnet ef migrations script AddSoftDelete AddAuditLog \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

Review the output SQL. Confirm:
- `CREATE TABLE audit_logs (...)` appears in the forward script.
- `DROP RULE IF EXISTS ...` and `DROP TABLE IF EXISTS audit_logs` appear in the rollback.

### 5. Regenerate the Cumulative Idempotent SQL Script

```bash
dotnet ef migrations script \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api \
  --idempotent \
  --output infra/postgres/migrations.sql
```

**Expected output:**

```
Build succeeded.
```

The file `infra/postgres/migrations.sql` will be updated with three migration blocks:

| # | Migration | Tables affected |
|---|-----------|----------------|
| 1 | InitialCreate | 15 core tables |
| 2 | AddSoftDelete | 6 auditable tables + xmin on 2 |
| 3 | AddAuditLog | `audit_logs` + 3 indexes + 2 rules |

### 6. Restart the API

```powershell
Start-Process "dotnet" -ArgumentList "run --project src/HealthPlatform.Api --launch-profile http" -WorkingDirectory "C:\Propel\PropelIQ"
```

### 7. Verify Build and Migration List

```bash
dotnet build HealthPlatform.sln
dotnet ef migrations list \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

**Expected migration list:**

```
20260526100151_InitialCreate
20260526105355_AddSoftDelete
<timestamp>_AddAuditLog
```

---

## Verification Checklist

- [ ] Migration file `<timestamp>_AddAuditLog.cs` generated in `Persistence/Migrations/`
- [ ] `audit_logs` table DDL includes all 9 columns: `id`, `user_id`, `action`, `entity_type`, `entity_id`, `timestamp`, `details` (jsonb), `previous_hash`, `current_hash`
- [ ] Three indexes created: `ix_audit_logs_entity_id`, `ix_audit_logs_user_id`, `ix_audit_logs_timestamp`
- [ ] `Up()` contains `CREATE OR REPLACE RULE audit_logs_no_update` and `audit_logs_no_delete`
- [ ] `Down()` drops both rules before dropping the table
- [ ] `infra/postgres/migrations.sql` regenerated and contains all 3 migration blocks
- [ ] `dotnet ef migrations list` shows 3 migrations in order
- [ ] `dotnet build` passes — 0 errors, 0 warnings
- [ ] 7-year retention comment present in migration `Up()` (no automated purge configured)
