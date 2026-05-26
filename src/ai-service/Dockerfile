# ─── Stage 1: base (shared system dependencies) ───────────────────────────────
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

# ─── Stage 2: deps (pip install into an isolated venv) ────────────────────────
FROM base AS deps

# Create isolated virtual environment so it can be copied cleanly to release
RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

# Install production deps first for better layer caching
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

# Copy source — overlaid by bind-mount volume in Compose so edits are
# picked up immediately without rebuilding the image.
COPY . .

EXPOSE 8000

# uvicorn --reload watches /app for .py changes and restarts the server
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]

# ─── Stage 4: release (lean runtime — no pip, no dev deps) ───────────────────
FROM base AS release

ENV PATH="/opt/venv/bin:$PATH"
ENV PYTHONDONTWRITEBYTECODE=1
ENV PYTHONUNBUFFERED=1

# Copy only the pre-built venv — pip itself stays in the deps stage
COPY --from=deps /opt/venv /opt/venv

# Run as non-root user (security hardening — OWASP A05)
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

COPY --chown=appuser:appgroup . .

EXPOSE 8000
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "1"]
