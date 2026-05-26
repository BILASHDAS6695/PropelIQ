# Task 001: Docker Compose Orchestration File

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-004 |
| **Epic** | EP-TECH |
| **Layer** | DevOps / Compose |
| **Priority** | High |
| **Estimated Effort** | 2 hours |
| **Dependencies** | None (first task) |

## Objective

Create the root `docker-compose.yml` that defines all five development services (`api`, `web`, `ai`, `postgres`, `redis`), a shared bridge network, named volumes for persistence, and an `.env` file reference — enabling `docker-compose up` to bring up the full stack.

## Implementation Steps

### 1. Create Root Directory Layout

```
(repo root)/
├── docker-compose.yml
├── .env.example          ← populated in Task 005
├── .env                  ← gitignored, created from .env.example
└── infra/
    └── postgres/
        └── init.sql      ← populated in Task 004
```

### 2. Create `docker-compose.yml`

**File:** `docker-compose.yml`

```yaml
version: "3.9"

# ─── Networks ──────────────────────────────────────────────────────────────────
networks:
  healthplatform:
    driver: bridge

# ─── Volumes ───────────────────────────────────────────────────────────────────
volumes:
  postgres_data:
  redis_data:

# ─── Services ──────────────────────────────────────────────────────────────────
services:

  # ── PostgreSQL ────────────────────────────────────────────────────────────────
  postgres:
    image: postgres:16-alpine
    container_name: hp_postgres
    restart: unless-stopped
    env_file: .env
    environment:
      POSTGRES_USER:     ${POSTGRES_USER}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD}
      POSTGRES_DB:       ${POSTGRES_DB}
    volumes:
      - postgres_data:/var/lib/postgresql/data
      - ./infra/postgres/init.sql:/docker-entrypoint-initdb.d/init.sql:ro
    ports:
      - "${POSTGRES_PORT:-5432}:5432"
    networks:
      - healthplatform
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${POSTGRES_USER} -d ${POSTGRES_DB}"]
      interval: 5s
      timeout: 5s
      retries: 10

  # ── Redis ──────────────────────────────────────────────────────────────────────
  redis:
    image: redis:7-alpine
    container_name: hp_redis
    restart: unless-stopped
    volumes:
      - redis_data:/data
    ports:
      - "${REDIS_PORT:-6379}:6379"
    networks:
      - healthplatform
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 10

  # ── .NET API ───────────────────────────────────────────────────────────────────
  api:
    build:
      context: ./src
      dockerfile: HealthPlatform.Api/Dockerfile
      target: development
    container_name: hp_api
    restart: unless-stopped
    env_file: .env
    environment:
      ASPNETCORE_ENVIRONMENT:      Development
      ASPNETCORE_URLS:             http://+:5013
      ConnectionStrings__DefaultConnection: >-
        Host=postgres;Port=5432;Database=${POSTGRES_DB};
        Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}
    volumes:
      - ./src:/app/src:cached
    ports:
      - "${API_PORT:-5013}:5013"
    networks:
      - healthplatform
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:5013/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 6
      start_period: 30s

  # ── Python AI Service ──────────────────────────────────────────────────────────
  ai:
    build:
      context: ./src/ai-service
      dockerfile: Dockerfile
      target: development
    container_name: hp_ai
    restart: unless-stopped
    env_file: .env
    environment:
      INTERNAL_API_KEY: ${INTERNAL_API_KEY}
      PORT:             ${AI_PORT:-8000}
      LOG_LEVEL:        ${LOG_LEVEL:-INFO}
    volumes:
      - ./src/ai-service:/app:cached
    ports:
      - "${AI_PORT:-8000}:8000"
    networks:
      - healthplatform
    healthcheck:
      test: ["CMD-SHELL", "curl -f http://localhost:8000/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 6
      start_period: 20s

  # ── Angular Dev Server ─────────────────────────────────────────────────────────
  web:
    build:
      context: ./src/health-platform-ui
      dockerfile: Dockerfile
      target: development
    container_name: hp_web
    restart: unless-stopped
    environment:
      NODE_ENV: development
    volumes:
      - ./src/health-platform-ui:/app:cached
      - /app/node_modules          # anonymous volume — do not overwrite node_modules
    ports:
      - "${WEB_PORT:-4200}:4200"
    networks:
      - healthplatform
    depends_on:
      api:
        condition: service_healthy
```

> **Note:** Angular uses `proxy.conf.json` (already in place from US-002) to proxy `/api` → `http://api:5013/api` inside the container network. The `web` service depends on `api` being healthy so the proxy target is guaranteed to be reachable on start.

### 3. Confirm `proxy.conf.json` targets container service name

The Angular proxy config must reference the Docker service name `api`, not `localhost`, when running inside Compose:

**File:** `src/health-platform-ui/proxy.conf.json`

```json
{
  "/api": {
    "target": "http://api:5013",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

> Keep a `proxy.conf.local.json` with `"target": "https://localhost:5013"` for native (non-Docker) development so switching between modes is a one-flag change.

## Acceptance Criteria

- [ ] `docker-compose.yml` exists at repo root with services: `api`, `web`, `ai`, `postgres`, `redis`
- [ ] All five services joined to the `healthplatform` bridge network
- [ ] `postgres_data` and `redis_data` named volumes declared
- [ ] `env_file: .env` set on all services that require secrets
- [ ] `depends_on` with `condition: service_healthy` on `api` (waits for postgres + redis)
- [ ] `web` depends on `api` being healthy
- [ ] All service ports mapped to host via env-var defaults

## Verification

```bash
# Validate compose syntax
docker compose config --quiet

# Confirm service names are present
docker compose config --services
# Expected output (any order): postgres redis api ai web
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-033 | Docker dev environment |
| TR-034 | Environment configuration |
| US-004 AC-1 | docker-compose.yml defines all 5 services |
| US-004 AC-7 | All services on shared Docker network |
| US-004 AC-8 | Environment variables loaded from .env |
