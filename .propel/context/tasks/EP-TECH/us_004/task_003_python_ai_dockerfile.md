# Task 003: Python AI Service Dockerfile (Multi-Stage)

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

Create a multi-stage Dockerfile for the Python FastAPI AI sidecar that produces a `development` stage with `uvicorn --reload` hot-reload and a lean `release` stage for production.

## Implementation Steps

### 1. Create the Dockerfile

**File:** `src/ai-service/Dockerfile`

```dockerfile
# ─── Stage 1: base (shared system deps) ───────────────────────────────────────
FROM python:3.11-slim AS base

# System dependencies for Tesseract OCR and image processing
RUN apt-get update && apt-get install -y --no-install-recommends \
    tesseract-ocr \
    tesseract-ocr-eng \
    libglib2.0-0 \
    libsm6 \
    libxrender1 \
    libxext6 \
    curl \
  && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# ─── Stage 2: deps (pip install into a venv) ──────────────────────────────────
FROM base AS deps

# Create isolated virtual environment
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Install production deps first (better layer caching)
COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt

# ─── Stage 3: development (uvicorn --reload + dev deps) ───────────────────────
FROM deps AS development

ENV PATH="/opt/venv/bin:$PATH"
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Install dev deps (pytest, httpx, etc.)
COPY requirements-dev.txt .
RUN pip install --no-cache-dir -r requirements-dev.txt

# Copy source — will be overlaid by bind-mount volume in Compose
COPY . .

EXPOSE 8000

# uvicorn --reload watches for .py file changes and restarts the server
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]

# ─── Stage 4: release ─────────────────────────────────────────────────────────
FROM base AS release

ENV PATH="/opt/venv/bin:$PATH"
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Copy venv from deps stage — no pip in final image
COPY --from=deps /opt/venv /opt/venv

# Run as non-root user
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --chown=appuser:appgroup . .

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
```

### 2. Create `.dockerignore` for the `ai-service` build context

**File:** `src/ai-service/.dockerignore`

```
# Python cache
__pycache__/
*.pyc
*.pyo
*.pyd
.pytest_cache/
.coverage
htmlcov/

# Virtual environments
.venv/
venv/
env/

# Secrets — never bake into image
.env

# IDE
.vscode/
.idea/

# Test artefacts
tests/
```

> **Note:** Excluding `tests/` from the release image keeps it lean. The `development` stage inherits them via the bind-mount volume, so pytest still runs against the live source tree.

### 3. Confirm `--reload` watches the correct directory

`uvicorn --reload` watches the working directory (`/app`) by default. The Compose bind-mount `./src/ai-service:/app:cached` means any `.py` edit on the host is immediately visible inside the container and triggers a reload.

Verify in container logs:

```bash
docker logs hp_ai --follow
# Expected: INFO:     Will watch for changes in these directories: ['/app']
# After editing a .py file:
# WARNING:  StatReload detected changes in '...'. Reloading...
```

### 4. Tesseract availability check

The `development` stage installs `tesseract-ocr` system-wide. Confirm it is reachable:

```bash
docker exec hp_ai tesseract --version
# Expected: tesseract 5.x.x
```

## Acceptance Criteria

- [ ] `src/ai-service/Dockerfile` exists with stages: `base`, `deps`, `development`, `release`
- [ ] `development` stage runs `uvicorn --reload` on port 8000
- [ ] `release` stage copies only the venv (not pip) and runs as non-root
- [ ] `tesseract-ocr` is installed in the `base` stage
- [ ] `src/ai-service/.dockerignore` excludes `__pycache__`, `.env`, `.venv`, `tests/`
- [ ] `docker build --target development -t hp-ai-dev .` succeeds from `src/ai-service/`
- [ ] Container responds on `GET /health` after compose start

## Verification

```bash
# Build just the development stage
docker build --target development -t hp-ai-dev src/ai-service/

# Smoke-test the image
docker run --rm -e INTERNAL_API_KEY=test -p 8000:8000 hp-ai-dev &
sleep 5
curl -s http://localhost:8000/health
# Expected: {"status":"healthy","service":"ai-service","version":"1.0.0"}
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-004 AC-5 | Python AI service container builds and starts |
| TR-033 | Docker dev environment |
