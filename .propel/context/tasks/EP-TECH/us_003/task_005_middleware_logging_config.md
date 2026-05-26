# Task 005: Middleware, Structured Logging & Service Configuration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-003 |
| **Epic** | EP-TECH |
| **Layer** | Python / Cross-Cutting |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001, Task 002, Task 003, Task 004 |

## Objective

Implement internal API key authentication middleware, configure structured JSON logging with `structlog`, disable CORS (internal-only service), and verify the fully wired service starts cleanly via `uvicorn app.main:app`.

## Implementation Steps

### 1. Implement Internal API Key Authentication Middleware

The sidecar is a private internal service — no public exposure. All requests must carry the `X-Internal-Api-Key` header matching the server-side secret. Reject without leaking details.

**File:** `src/ai-service/app/middleware/api_key_auth.py`

```python
"""Internal API key authentication middleware.

Rejects requests that do not present the correct X-Internal-Api-Key header.
Returns 401 Unauthorized without leaking the expected key value.
"""

from __future__ import annotations

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse

from app.config import settings

_HEADER_NAME = "X-Internal-Api-Key"
_EXEMPT_PATHS = {"/health"}


class ApiKeyMiddleware(BaseHTTPMiddleware):
    """Validates the X-Internal-Api-Key header on every non-exempt request."""

    async def dispatch(self, request: Request, call_next):  # type: ignore[override]
        if request.url.path in _EXEMPT_PATHS:
            return await call_next(request)

        api_key = request.headers.get(_HEADER_NAME)
        if not api_key or api_key != settings.internal_api_key:
            return JSONResponse(
                status_code=401,
                content={"detail": "Unauthorized"},
            )

        return await call_next(request)
```

> **Security note:** The `/health` endpoint is intentionally exempt so load-balancer probes can reach it without credentials. All processing endpoints (`/extraction/*`, `/coding/*`, `/intake/*`) are protected.

### 2. Configure Structured JSON Logging

**File:** `src/ai-service/app/logging_config.py`

```python
"""Structured JSON logging configuration using structlog."""

from __future__ import annotations

import logging
import sys

import structlog


def configure_logging(log_level: str = "INFO") -> None:
    """Configure structlog for JSON-formatted output.

    Args:
        log_level: Python logging level string (DEBUG, INFO, WARNING, ERROR).
    """
    logging.basicConfig(
        format="%(message)s",
        stream=sys.stdout,
        level=getattr(logging, log_level.upper(), logging.INFO),
    )

    structlog.configure(
        processors=[
            structlog.contextvars.merge_contextvars,
            structlog.stdlib.add_log_level,
            structlog.stdlib.add_logger_name,
            structlog.processors.TimeStamper(fmt="iso"),
            structlog.processors.StackInfoRenderer(),
            structlog.processors.format_exc_info,
            structlog.processors.JSONRenderer(),
        ],
        wrapper_class=structlog.make_filtering_bound_logger(
            getattr(logging, log_level.upper(), logging.INFO)
        ),
        context_class=dict,
        logger_factory=structlog.PrintLoggerFactory(),
    )
```

### 3. Wire Middleware and Logging into `main.py`

**File:** `src/ai-service/app/main.py` (final version)

```python
"""HealthPlatform AI Service — FastAPI entry point."""

from __future__ import annotations

from fastapi import FastAPI
from fastapi.responses import JSONResponse

from app.config import settings
from app.logging_config import configure_logging
from app.middleware.api_key_auth import ApiKeyMiddleware
from app.routers import extraction, coding, intake

# Configure structured logging as early as possible
configure_logging(settings.log_level)

import structlog  # noqa: E402 — must import after configure_logging()
logger = structlog.get_logger(__name__)

app = FastAPI(
    title="HealthPlatform AI Service",
    version="1.0.0",
    docs_url=None,    # Disable Swagger UI — internal service
    redoc_url=None,
)

# Middleware (registered in reverse order — auth runs first)
app.add_middleware(ApiKeyMiddleware)

# Routers
app.include_router(extraction.router, prefix="/extraction", tags=["Extraction"])
app.include_router(coding.router,    prefix="/coding",     tags=["Coding"])
app.include_router(intake.router,    prefix="/intake",     tags=["Intake"])


@app.get("/health", status_code=200)
async def health_check() -> JSONResponse:
    logger.info("health_check_called")
    return JSONResponse(
        status_code=200,
        content={
            "status": "healthy",
            "service": "ai-service",
            "version": "1.0.0",
        },
    )


@app.on_event("startup")
async def on_startup() -> None:
    logger.info("ai_service_starting", port=settings.port, log_level=settings.log_level)


@app.on_event("shutdown")
async def on_shutdown() -> None:
    logger.info("ai_service_stopping")
```

### 4. Disable CORS

CORS is **not configured** — no `CORSMiddleware` is added. The service is internal-only: it accepts connections only from the .NET backend on the same host or internal network. No browser client will contact it directly.

Confirm that `fastapi.middleware.cors.CORSMiddleware` is **absent** from `main.py`.

### 5. Add `pydantic-settings` Dependency

Ensure `requirements.txt` includes the `pydantic-settings` package required by `app/config.py`:

**File:** `src/ai-service/requirements.txt` (add line)

```
pydantic-settings==2.2.1
```

### 6. Verify Full Service Start

```bash
cd src/ai-service
cp .env.example .env
# Edit .env — set INTERNAL_API_KEY to any test value, e.g.:
# INTERNAL_API_KEY=local-dev-key

uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Expected startup log output (JSON):

```json
{"event": "ai_service_starting", "port": 8000, "log_level": "INFO", "level": "info", "timestamp": "..."}
```

### 7. Verify Authentication Enforcement

```bash
# Should return 401
curl -s http://localhost:8000/extraction/ocr -X POST | python -m json.tool
# Expected: {"detail":"Unauthorized"}

# Health check is exempt — should return 200
curl -s http://localhost:8000/health | python -m json.tool
# Expected: {"status":"healthy","service":"ai-service","version":"1.0.0"}

# Should pass with correct key
curl -s http://localhost:8000/coding/suggest \
  -X POST \
  -H "X-Internal-Api-Key: local-dev-key" \
  -H "Content-Type: application/json" \
  -d '{"clinical_text": "chest pain"}' | python -m json.tool
```

## Acceptance Criteria

- [ ] `ApiKeyMiddleware` rejects requests without `X-Internal-Api-Key` header with HTTP 401
- [ ] `ApiKeyMiddleware` allows requests with the correct key value
- [ ] `/health` endpoint bypasses authentication (no key required)
- [ ] Structured JSON logging is configured via `structlog` on service startup
- [ ] Log entries are emitted in JSON format to stdout
- [ ] `CORSMiddleware` is **not** present in `main.py`
- [ ] Service starts successfully with `uvicorn app.main:app`
- [ ] Startup log message emitted at INFO level confirming port and log level

## Verification

```bash
cd src/ai-service
# Start service
uvicorn app.main:app --port 8000 &

# Health check (no key needed)
curl -f http://localhost:8000/health

# Auth rejection
RESP=$(curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:8000/extraction/ocr)
test "$RESP" = "401" && echo "Auth middleware: PASS" || echo "Auth middleware: FAIL"

# Auth acceptance
RESP=$(curl -s -o /dev/null -w "%{http_code}" -X POST http://localhost:8000/extraction/ocr \
  -H "X-Internal-Api-Key: local-dev-key" \
  -H "Content-Type: application/json" \
  -d '{"document_base64":"dGVzdA==","format":"pdf"}')
test "$RESP" != "401" && echo "Key accepted: PASS" || echo "Key accepted: FAIL"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-003 AC-5 | Internal API key authentication middleware configured |
| US-003 AC-6 | Structured logging configured (JSON format) |
| US-003 AC-7 | CORS disabled (internal-only service) |
| US-003 AC-8 | Service starts successfully with `uvicorn app.main:app` |
| AIR-025 | Data privacy — no external AI transmission |
| ADR-003 | Python Sidecar for AI |
