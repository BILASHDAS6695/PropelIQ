# Task 002: .NET API Dockerfile (Multi-Stage)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-004 |
| **Epic** | EP-TECH |
| **Layer** | DevOps / Container |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001 |

## Objective

Create a multi-stage Dockerfile for the .NET 8 API that produces a lean `development` stage with `dotnet watch` hot-reload and a `release` stage for production use.

## Implementation Steps

### 1. Create the Dockerfile

**File:** `src/HealthPlatform.Api/Dockerfile`

```dockerfile
# ─── Stage 1: restore ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS restore
WORKDIR /app

# Copy solution and project files first to exploit Docker layer caching
COPY HealthPlatform.sln ./
COPY HealthPlatform.Api/HealthPlatform.Api.csproj             ./HealthPlatform.Api/
COPY HealthPlatform.Application/HealthPlatform.Application.csproj ./HealthPlatform.Application/
COPY HealthPlatform.Domain/HealthPlatform.Domain.csproj       ./HealthPlatform.Domain/
COPY HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj ./HealthPlatform.Infrastructure/
COPY HealthPlatform.Tests/HealthPlatform.Tests.csproj         ./HealthPlatform.Tests/

RUN dotnet restore HealthPlatform.sln

# ─── Stage 2: development (hot-reload via dotnet watch) ───────────────────────
FROM restore AS development
WORKDIR /app

# Copy full source — will be overlaid by a bind-mount volume in Compose
COPY . .

EXPOSE 5013

# dotnet watch rebuilds and restarts the process on any .cs file change
CMD ["dotnet", "watch", "--project", "HealthPlatform.Api/HealthPlatform.Api.csproj", \
     "run", "--urls", "http://+:5013", "--no-launch-profile"]

# ─── Stage 3: build (CI / release candidate) ──────────────────────────────────
FROM restore AS build
WORKDIR /app
COPY . .
RUN dotnet publish HealthPlatform.Api/HealthPlatform.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ─── Stage 4: release (smallest runtime image) ────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS release
WORKDIR /app

# Run as non-root user (security hardening)
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser

COPY --from=build /app/publish .

EXPOSE 5013
ENTRYPOINT ["dotnet", "HealthPlatform.Api.dll"]
```

### 2. Create `.dockerignore` for the `src/` build context

**File:** `src/.dockerignore`

```
# Build outputs
**/bin/
**/obj/
**/.vs/

# Test results
**/TestResults/

# IDE / tooling
**/.vscode/
**/*.user
**/.DS_Store

# Node (frontend — not needed in .NET build context)
health-platform-ui/node_modules/
health-platform-ui/.angular/
health-platform-ui/dist/

# Python AI service — separate build context
ai-service/
```

### 3. Verify hot-reload works with volume mount

When `docker compose up` starts the `api` service, the bind mount `./src:/app/src:cached` overlays the container's `/app` directory. Confirm the `dotnet watch` CMD picks up changes:

```bash
# Inside the running container — expect watch output
docker logs hp_api --follow
# Look for: "watch  : Started"
```

Make a trivial change to any `.cs` file in `src/HealthPlatform.Api/` — the container should rebuild and restart automatically within ~5 seconds.

### 4. Confirm `appsettings.Development.json` does not hard-code localhost

The connection string for Postgres must resolve to the Docker service name `postgres` inside the container. Ensure `appsettings.Development.json` does **not** override the compose-injected env var:

**File:** `src/HealthPlatform.Api/appsettings.Development.json`

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

The `ConnectionStrings__DefaultConnection` value is injected entirely from `docker-compose.yml` environment variables — no change to `appsettings.json` is required.

## Acceptance Criteria

- [ ] `src/HealthPlatform.Api/Dockerfile` exists with stages: `restore`, `development`, `build`, `release`
- [ ] `development` stage uses `dotnet watch run` on port 5013
- [ ] `release` stage uses `dotnet/aspnet:8.0-alpine` (not SDK) and runs as non-root user
- [ ] `src/.dockerignore` excludes `bin/`, `obj/`, `node_modules/`, `ai-service/`
- [ ] `docker build --target development -t hp-api-dev .` succeeds from `src/`
- [ ] Container responds on port 5013 after compose start

## Verification

```bash
# Build just the development stage to validate the Dockerfile
docker build --target development -t hp-api-dev -f src/HealthPlatform.Api/Dockerfile src/

# Confirm image was created
docker images hp-api-dev
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-004 AC-4 | .NET API container builds and runs with hot-reload |
| TR-033 | Docker dev environment |
