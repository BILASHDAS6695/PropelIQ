# Task 002: Router Structure & Health Check Endpoint

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-003 |
| **Epic** | EP-TECH |
| **Layer** | Python / API |
| **Priority** | Critical |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001 |

## Objective

Create the three domain routers (`extraction`, `coding`, `intake`) with stub route handlers, implement the `/health` endpoint returning 200 with service status, and register all routers on the FastAPI application instance.

## Implementation Steps

### 1. Implement the Health Check Endpoint

Add a `/health` route directly on the app instance in `main.py`. It must return HTTP 200 with a JSON body describing the service status.

**File:** `src/ai-service/app/main.py` (extend from Task 001 skeleton)

```python
import logging
from fastapi import FastAPI
from fastapi.responses import JSONResponse

from app.routers import extraction, coding, intake
from app.middleware.api_key_auth import ApiKeyMiddleware
from app.config import settings

app = FastAPI(
    title="HealthPlatform AI Service",
    version="1.0.0",
    docs_url=None,
    redoc_url=None,
)

# Routers
app.include_router(extraction.router, prefix="/extraction", tags=["Extraction"])
app.include_router(coding.router,    prefix="/coding",     tags=["Coding"])
app.include_router(intake.router,    prefix="/intake",     tags=["Intake"])


@app.get("/health", status_code=200)
async def health_check() -> JSONResponse:
    return JSONResponse(
        status_code=200,
        content={
            "status": "healthy",
            "service": "ai-service",
            "version": "1.0.0",
        },
    )
```

### 2. Create the Extraction Router

**File:** `src/ai-service/app/routers/extraction.py`

```python
from fastapi import APIRouter

router = APIRouter()


@router.post("/ocr")
async def extract_ocr():
    """OCR text extraction from document images — placeholder."""
    return {"status": "not_implemented"}


@router.post("/ner")
async def extract_ner():
    """Named entity recognition from extracted text — placeholder."""
    return {"status": "not_implemented"}
```

### 3. Create the Coding Router

**File:** `src/ai-service/app/routers/coding.py`

```python
from fastapi import APIRouter

router = APIRouter()


@router.post("/suggest")
async def suggest_codes():
    """ICD/CPT code suggestions from clinical text — placeholder."""
    return {"status": "not_implemented"}


@router.post("/validate")
async def validate_codes():
    """Code validation against known code sets — placeholder."""
    return {"status": "not_implemented"}
```

### 4. Create the Intake Router

**File:** `src/ai-service/app/routers/intake.py`

```python
from fastapi import APIRouter

router = APIRouter()


@router.post("/parse")
async def parse_intake():
    """NLP parsing of intake form text — placeholder."""
    return {"status": "not_implemented"}


@router.post("/classify")
async def classify_intake():
    """Classification of intake data — placeholder."""
    return {"status": "not_implemented"}
```

### 5. Verify Router Registration

Start the service and confirm all routes are reachable:

```bash
uvicorn app.main:app --reload --port 8000
```

Verify endpoints with `curl` or `httpx`:

```bash
curl http://localhost:8000/health
# Expected: {"status":"healthy","service":"ai-service","version":"1.0.0"}

curl -X POST http://localhost:8000/extraction/ocr
curl -X POST http://localhost:8000/coding/suggest
curl -X POST http://localhost:8000/intake/parse
```

> **Note:** Until authentication middleware (Task 005) is wired, the stub endpoints are unauthenticated. Do not expose the service publicly before middleware is in place.

## Acceptance Criteria

- [ ] `GET /health` returns HTTP 200 with `{"status": "healthy", "service": "ai-service", "version": "1.0.0"}`
- [ ] Router prefix `/extraction` is registered and reachable
- [ ] Router prefix `/coding` is registered and reachable
- [ ] Router prefix `/intake` is registered and reachable
- [ ] `app.include_router()` calls are present in `main.py` for all three routers
- [ ] Service starts without errors via `uvicorn app.main:app`

## Verification

```bash
cd src/ai-service
uvicorn app.main:app --port 8000 &
curl -s http://localhost:8000/health | python -m json.tool
```

Expected output:

```json
{
    "status": "healthy",
    "service": "ai-service",
    "version": "1.0.0"
}
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-003 AC-1 | FastAPI project with router structure (extraction, coding, intake) |
| US-003 AC-2 | Health check endpoint at `/health` returns 200 with service status |
| ADR-003 | Python Sidecar for AI |
