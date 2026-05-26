# HealthPlatform AI Service

Internal Python FastAPI sidecar for AI processing (OCR, NER, medical coding, intake NLP).

## Requirements

- Python 3.11+
- Tesseract OCR installed on the host

## Local Setup

```bash
python -m venv .venv
# Windows
.\.venv\Scripts\Activate.ps1
# macOS/Linux
source .venv/bin/activate

pip install -r requirements-dev.txt
```

## Configuration

Copy `.env.example` to `.env` and set values:

```bash
cp .env.example .env
```

| Variable | Description | Default |
|---|---|---|
| `INTERNAL_API_KEY` | Shared secret for `.NET` → sidecar auth | *(required)* |
| `PORT` | Listening port | `8000` |
| `LOG_LEVEL` | Python log level | `INFO` |

## Run

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

## Test

```bash
pytest tests/
```
