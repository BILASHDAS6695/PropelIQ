# Task 001: Project Structure & Dependencies

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-003 |
| **Epic** | EP-TECH |
| **Layer** | Python / Build |
| **Priority** | Critical |
| **Estimated Effort** | 1 hour |
| **Dependencies** | None (first task) |

## Objective

Scaffold the Python FastAPI AI sidecar project with the correct directory structure, virtual environment, and all required dependencies declared in `requirements.txt`.

## Implementation Steps

### 1. Create Project Directory Structure

```
src/
└── ai-service/
    ├── app/
    │   ├── __init__.py
    │   ├── main.py
    │   ├── config.py
    │   ├── routers/
    │   │   ├── __init__.py
    │   │   ├── extraction.py
    │   │   ├── coding.py
    │   │   └── intake.py
    │   ├── services/
    │   │   ├── __init__.py
    │   │   ├── ocr_service.py
    │   │   ├── ner_service.py
    │   │   ├── coding_service.py
    │   │   └── intake_service.py
    │   ├── models/
    │   │   ├── __init__.py
    │   │   ├── extraction_models.py
    │   │   ├── coding_models.py
    │   │   └── intake_models.py
    │   └── middleware/
    │       ├── __init__.py
    │       └── api_key_auth.py
    ├── tests/
    │   ├── __init__.py
    │   └── test_health.py
    ├── requirements.txt
    ├── requirements-dev.txt
    ├── .env.example
    └── README.md
```

### 2. Create `requirements.txt`

**File:** `src/ai-service/requirements.txt`

```
fastapi==0.111.0
uvicorn[standard]==0.29.0
pydantic==2.7.1
spacy==3.7.4
pytesseract==0.3.10
pymupdf==1.24.3
python-multipart==0.0.9
python-dotenv==1.0.1
structlog==24.2.0
```

### 3. Create `requirements-dev.txt`

**File:** `src/ai-service/requirements-dev.txt`

```
-r requirements.txt
pytest==8.2.0
pytest-asyncio==0.23.6
httpx==0.27.0
```

### 4. Create Minimal `app/main.py` Entry Point

**File:** `src/ai-service/app/main.py`

```python
from fastapi import FastAPI

app = FastAPI(
    title="HealthPlatform AI Service",
    version="1.0.0",
    docs_url=None,   # Disable Swagger UI in all envs (internal service)
    redoc_url=None,
)
```

### 5. Create `app/__init__.py` and Package Init Files

Create empty `__init__.py` files for all packages:

```bash
touch app/__init__.py
touch app/routers/__init__.py
touch app/services/__init__.py
touch app/models/__init__.py
touch app/middleware/__init__.py
touch tests/__init__.py
```

### 6. Create `.env.example`

**File:** `src/ai-service/.env.example`

```
# Internal API key — override in deployment secrets
INTERNAL_API_KEY=changeme
# Listening port
PORT=8000
# Log level: DEBUG | INFO | WARNING | ERROR
LOG_LEVEL=INFO
```

### 7. Create `app/config.py`

**File:** `src/ai-service/app/config.py`

```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    internal_api_key: str
    port: int = 8000
    log_level: str = "INFO"

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()
```

> **Note:** Add `pydantic-settings==2.2.1` to `requirements.txt`.

### 8. Set Up Virtual Environment (local development)

```bash
cd src/ai-service
python -m venv .venv
# Windows
.\.venv\Scripts\Activate.ps1
# macOS/Linux
source .venv/bin/activate
pip install -r requirements-dev.txt
```

## Acceptance Criteria

- [ ] `src/ai-service/` directory exists with the structure above
- [ ] `requirements.txt` includes: `fastapi`, `uvicorn`, `spacy`, `pytesseract`, `pymupdf`, `pydantic==2.x`, `structlog`, `python-dotenv`
- [ ] `app/main.py` creates a `FastAPI` instance
- [ ] All Python packages are importable after `pip install -r requirements.txt`
- [ ] `.env.example` documents required environment variables

## Verification

```bash
cd src/ai-service
pip install -r requirements.txt
python -c "import fastapi, uvicorn, spacy, fitz, pytesseract, structlog; print('All imports OK')"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-003 AC-3 | requirements.txt includes fastapi, uvicorn, spacy, pytesseract, pymupdf |
| ADR-003 | Python Sidecar for AI |
| ADR-004 | Local AI Inference — no external API calls |
