# Task 004: EF Core InitialCreate Migration and Connection Health Verification

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-008 |
| **Epic** | EP-DATA |
| **Layer** | Infrastructure / DevOps |
| **Priority** | Critical |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001, Task 002, Task 003 (all must be complete and building) |

## Objective

Generate the `InitialCreate` EF Core migration from the fully-configured `ApplicationDbContext`
and verify that:

1. The migration file is created and compiles without errors.
2. The migration can be applied against the local Docker PostgreSQL instance.
3. The API `/health` endpoint returns a healthy status for both `postgres` and `efcore`
   health checks after the migration is applied.

## Acceptance Criteria Covered

- AC-1: PostgreSQL 16 database provisioned — Docker container running and reachable
- AC-3: Connection string configured via environment variables — verified by Docker Compose
  override (`ConnectionStrings__DefaultConnection`) and local `appsettings.json` template
- AC-7: `dotnet ef migrations add InitialCreate` executes successfully
- AC-8: Database responds to connection test from the API health check

---

## Implementation Steps

### 1. Ensure Docker PostgreSQL is Running

```bash
docker compose up postgres -d
```

Wait for the health check to pass:

```bash
docker compose ps postgres
# Expected: Status = healthy
```

### 2. Restore Tools and Verify `dotnet-ef` is Available

The EF Core CLI tool must be installed globally (or locally via a tool manifest). If not present:

```bash
dotnet tool install --global dotnet-ef --version 8.*
```

Confirm version:

```bash
dotnet ef --version
# Expected: Entity Framework Core .NET Command-line Tools 8.x.x
```

### 3. Generate the InitialCreate Migration

Run from the `src/` directory (the solution root):

```bash
cd src
dotnet ef migrations add InitialCreate \
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

**Expected files created:**

```
src/HealthPlatform.Infrastructure/Persistence/Migrations/
    <timestamp>_InitialCreate.cs
    <timestamp>_InitialCreate.Designer.cs
    ApplicationDbContextModelSnapshot.cs
```

> **Snake_case check**: Open `<timestamp>_InitialCreate.cs` and confirm table names use
> underscores (e.g., `patient_profiles`, `appointment_slots`) and column names are
> snake_case (e.g., `created_at`, `insurance_member_id`). This confirms Task 001's
> `UseSnakeCaseNamingConvention()` is active.

> **JSONB check**: Confirm `DataJson`, `ConsolidatedDataJson`, and `Details` columns show
> `type: "jsonb"` in the migration — not `text`.

### 4. Apply the Migration to the Local Database

```bash
cd src
dotnet ef database update \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

**Expected output:**

```
Build started...
Build succeeded.
Applying migration '<timestamp>_InitialCreate'.
Done.
```

### 5. Verify Health Check Endpoint

Start the API (ensure Docker PostgreSQL is still running):

```bash
cd src
dotnet run --project HealthPlatform.Api
```

In a separate terminal, call the detailed health check endpoint:

```bash
curl -s http://localhost:5013/health/detail | python -m json.tool
```

**Expected response** (HTTP 200):

```json
{
  "status": "Healthy",
  "entries": {
    "postgres": { "status": "Healthy" },
    "efcore":   { "status": "Healthy" },
    "redis":    { "status": "Healthy" }
  }
}
```

> If Redis is not running locally, the `redis` entry may be `Unhealthy`. Only `postgres`
> and `efcore` are in scope for this user story.

### 6. Confirm `Maximum Pool Size` Is Active

Connect to the running PostgreSQL container and inspect active connections:

```bash
docker exec -it hp_postgres psql -U postgres -d healthplatform \
  -c "SELECT count(*) FROM pg_stat_activity WHERE datname = 'healthplatform';"
```

Ensure the count does not exceed 100 under load. At idle, it should show 1–2 connections
(the health check connections).

---

## Connection String Environment-Variable Override Reference

| Method | Syntax | Used in |
|--------|--------|---------|
| Docker Compose | `ConnectionStrings__DefaultConnection: Host=postgres;...` | `docker-compose.yml` |
| Environment variable (local shell) | `export ConnectionStrings__DefaultConnection="Host=..."` | Developer machine |
| `appsettings.Development.json` | `"DefaultConnection": "Host=localhost;..."` | Local dev (fallback) |

ASP.NET Core's configuration system reads environment variables with double-underscore (`__`)
as section separators, which means `ConnectionStrings__DefaultConnection` maps to
`ConnectionStrings:DefaultConnection` — no hardcoded credentials are required in production.

---

## Rollback

If the migration needs to be undone:

```bash
cd src
dotnet ef database update 0 \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api

dotnet ef migrations remove \
  --project HealthPlatform.Infrastructure \
  --startup-project HealthPlatform.Api
```

---

## Verification Checklist

- [ ] `dotnet ef migrations add InitialCreate` completes without errors
- [ ] Migration file contains snake_case table and column names
- [ ] Migration file contains `jsonb` column type for JSON properties
- [ ] `dotnet ef database update` applies the migration successfully
- [ ] `GET /health/detail` returns `postgres: Healthy` and `efcore: Healthy`
- [ ] No connection string credentials are hardcoded in committed files
