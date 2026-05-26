# Task 005: Environment Configuration, DB Init & Health Check Dependencies

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-004 |
| **Epic** | EP-TECH |
| **Layer** | DevOps / Configuration |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001, Task 002, Task 003, Task 004 |

## Objective

Create the root `.env.example` with all variables required by the Compose stack, write the PostgreSQL initialisation script, and validate that the full stack starts within 60 seconds with all health-check dependencies correctly sequenced.

## Implementation Steps

### 1. Create Root `.env.example`

**File:** `.env.example`

```dotenv
# ── PostgreSQL ─────────────────────────────────────────────────────────────────
POSTGRES_USER=healthplatform
POSTGRES_PASSWORD=changeme
POSTGRES_DB=healthplatform
POSTGRES_PORT=5432

# ── Redis ──────────────────────────────────────────────────────────────────────
REDIS_PORT=6379

# ── .NET API ───────────────────────────────────────────────────────────────────
API_PORT=5013
ASPNETCORE_ENVIRONMENT=Development

# ── Python AI Service ──────────────────────────────────────────────────────────
# REQUIRED: set a strong secret before running
INTERNAL_API_KEY=changeme
AI_PORT=8000
LOG_LEVEL=INFO

# ── Angular Dev Server ─────────────────────────────────────────────────────────
WEB_PORT=4200
```

### 2. Create `.env` from example (one-time developer setup)

```bash
cp .env.example .env
# Edit .env — set POSTGRES_PASSWORD and INTERNAL_API_KEY to non-default values
```

Ensure `.env` is listed in the root `.gitignore`:

**File:** `.gitignore` (add if not already present)

```
# Developer secrets
.env
```

### 3. Create PostgreSQL Initialisation Script

The Postgres container runs all `.sql` files in `/docker-entrypoint-initdb.d/` on first boot. The Compose mount `./infra/postgres/init.sql:/docker-entrypoint-initdb.d/init.sql:ro` wires this script in.

**File:** `infra/postgres/init.sql`

```sql
-- Ensure the database exists (Compose already creates it via POSTGRES_DB,
-- but this script is a safe no-op if it already exists)
SELECT 'CREATE DATABASE healthplatform'
WHERE NOT EXISTS (
    SELECT FROM pg_database WHERE datname = 'healthplatform'
)\gexec

-- Create application schema
\connect healthplatform

CREATE SCHEMA IF NOT EXISTS app;

-- Grant the application user privileges on the schema
GRANT USAGE  ON SCHEMA app TO CURRENT_USER;
GRANT CREATE ON SCHEMA app TO CURRENT_USER;
```

### 4. Add Root `.gitignore` Entries

**File:** `.gitignore` (repo root) — ensure these entries exist:

```
# Environment secrets
.env

# Docker build cache (Windows Docker Desktop)
.docker/
```

### 5. Validate Health-Check Dependency Chain

The intended startup order is:

```
postgres (healthy) ─┐
                     ├─► api (healthy) ─► web
redis    (healthy) ─┘
                        ai  (independent, no hard deps)
```

Verify the full stack starts cleanly:

```bash
# From repo root
cp .env.example .env
# Edit POSTGRES_PASSWORD and INTERNAL_API_KEY

docker compose up --build --detach

# Wait for all containers to reach healthy / running state
docker compose ps

# Expected (within 60s):
# hp_postgres   running (healthy)
# hp_redis      running (healthy)
# hp_api        running (healthy)
# hp_ai         running (healthy)
# hp_web        running
```

### 6. End-to-End Smoke Test

```bash
# Postgres reachable
docker exec hp_postgres pg_isready -U healthplatform -d healthplatform

# Redis reachable
docker exec hp_redis redis-cli ping
# Expected: PONG

# API health
curl -s http://localhost:5013/health
# Expected: 200 OK

# AI health
curl -s http://localhost:8000/health
# Expected: {"status":"healthy","service":"ai-service","version":"1.0.0"}

# Angular dev server
curl -s -o /dev/null -w "%{http_code}" http://localhost:4200
# Expected: 200
```

### 7. Confirm 60-Second Startup Target

```bash
time docker compose up --build --detach
# Measure wall-clock time from "docker compose up" to all services healthy
# Target: < 60 seconds on a warm (already-pulled) image set
```

### 8. Tear-Down

```bash
# Stop and remove containers (preserves named volumes)
docker compose down

# Stop and remove containers + volumes (full reset)
docker compose down --volumes
```

## Acceptance Criteria

- [ ] `.env.example` exists at repo root with all 9 variables documented
- [ ] `infra/postgres/init.sql` creates the `app` schema on first boot
- [ ] `.env` is gitignored at repo root
- [ ] `docker compose up --build` starts all 5 services without error
- [ ] All services are reachable on their declared host ports after start
- [ ] `api` does not start before `postgres` and `redis` are healthy
- [ ] `web` does not start before `api` is healthy
- [ ] Full stack reaches running/healthy state within 60 seconds (warm images)

## Verification

```bash
# Full integration check
docker compose up --build --detach
sleep 60
docker compose ps --format "table {{.Name}}\t{{.Status}}"

# Expect all rows to show "running" or "running (healthy)"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-004 AC-2 | PostgreSQL starts with initial database created |
| US-004 AC-3 | Redis starts with default configuration |
| US-004 AC-8 | Environment variables loaded from `.env` |
| US-004 AC-9 | `docker-compose up` starts all services within 60 seconds |
| US-004 AC-10 | Health check dependencies ensure API waits for DB and Redis |
| TR-034 | Environment configuration |
