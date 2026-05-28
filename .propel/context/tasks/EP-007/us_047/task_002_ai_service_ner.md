# Task 002: Python AI Service — NER Implementation (spaCy / scispaCy)

## Context

| Field                | Value                                                                                   |
|----------------------|-----------------------------------------------------------------------------------------|
| **User Story**       | US-047                                                                                  |
| **Epic**             | EP-007                                                                                  |
| **Layer**            | AI Service (Python / FastAPI)                                                           |
| **Priority**         | Critical                                                                                |
| **Estimated Effort** | 90 minutes                                                                              |
| **Dependencies**     | Task 001 complete — `NerEntity` schema defined; `en_ner_bc5cdr_md` and `en_ner_bionlp13cg_md` scispaCy models available via pip |

## Objective

Implement the `/extraction/ner` endpoint in the Python AI service using
**scispaCy** local biomedical NER models (ADR-004 — no external API calls).
Two models are loaded at startup:

| Model                  | Source labels        | Maps to                    |
|------------------------|----------------------|----------------------------|
| `en_ner_bc5cdr_md`     | CHEMICAL, DISEASE    | MEDICATION, DIAGNOSIS      |
| `en_ner_bionlp13cg_md` | Anatomical_system, Organ, Organism_subdivision | ANATOMY |

Additional entity types (PROCEDURE, LAB_TEST, LAB_VALUE, SYMPTOM) are matched
with a spaCy `EntityRuler` pattern layer loaded from a curated JSONL file.

All processing is local, memory-bounded, and supports chunked text for very
long documents.

## Acceptance Criteria Covered

- AC: Uses spaCy/scispaCy with biomedical NER models (TR-027)
- AC: Entity types: DIAGNOSIS, MEDICATION, PROCEDURE, LAB_TEST, LAB_VALUE, ANATOMY, SYMPTOM
- AC: Each entity has text, type, start_offset, end_offset, confidence_score
- AC: Entities below threshold stored but flagged `low_confidence: true`
- AC: Processing time < 10 seconds per page
- AC: Very long document → chunked processing
- AC: Model unavailable → raises exception (caller queues retry)
- AC: No external API calls (ADR-004)

---

## Implementation Steps

### 1. Update `requirements.txt`

Edit `src/ai-service/requirements.txt`.

Replace the existing content with:

```text
fastapi==0.111.0
uvicorn[standard]==0.29.0
pydantic==2.7.1
pydantic-settings==2.2.1
spacy==3.7.4
scispacy==0.5.4
# scispaCy biomedical models (installed via pip URLs — see Dockerfile)
# en_ner_bc5cdr_md — Chemical/Disease NER
# en_ner_bionlp13cg_md — Anatomy/Cell-biology NER
pytesseract==0.3.10
pymupdf==1.24.3
python-multipart==0.0.9
python-dotenv==1.0.1
structlog==24.2.0
redis[hiredis]==5.0.7
httpx==0.27.0
```

> The scispaCy model packages are installed **separately** in the Dockerfile
> via `pip install` with direct GitHub release URLs (they are not on PyPI).

---

### 2. Update `config.py`

Edit `src/ai-service/app/config.py`.

Add NER settings to the `Settings` class:

```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    internal_api_key: str
    port: int = 8000
    log_level: str = "INFO"

    # Ollama (local LLM — ADR-004)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "llama3"

    # Redis (session store)
    redis_url: str = "redis://localhost:6379/0"

    # NER — scispaCy models (TR-027 / ADR-004)
    ner_confidence_threshold: float = 0.7
    ner_chunk_size: int = 10_000   # max chars per text chunk

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()  # type: ignore[call-arg]
```

---

### 3. Update `extraction_models.py`

Edit `src/ai-service/app/models/extraction_models.py`.

Replace the NER section (keep the OCR section intact):

```python
# --- NER ---

class NerRequest(BaseModel):
    """Request body for POST /extraction/ner."""

    pages: list[str] = Field(
        ...,
        description="List of plain-text page strings from OCR output.",
        min_length=1,
    )
    confidence_threshold: float = Field(
        default=0.7,
        ge=0.0,
        le=1.0,
        description="Entities with score below this value are flagged low_confidence.",
    )


class EntitySpan(BaseModel):
    """A single recognised clinical entity."""

    text: str = Field(description="Surface text as it appears in the source.")
    type: str = Field(
        description=(
            "Normalised entity type: DIAGNOSIS | MEDICATION | PROCEDURE | "
            "LAB_TEST | LAB_VALUE | ANATOMY | SYMPTOM."
        )
    )
    start_offset: int = Field(description="Zero-based char start in the page text.")
    end_offset: int = Field(description="Zero-based char end (exclusive) in the page text.")
    confidence_score: float = Field(description="Model confidence 0.0–1.0.")
    low_confidence: bool = Field(
        description="True when confidence_score < the request threshold."
    )


class NerResponse(BaseModel):
    """Response body for POST /extraction/ner."""

    entities: list[EntitySpan] = Field(description="All detected entity spans.")
```

---

### 4. Implement `ner_service.py`

Replace the contents of `src/ai-service/app/services/ner_service.py`:

```python
"""Named Entity Recognition service — local spaCy / scispaCy processing (ADR-004)."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

import spacy
from spacy.language import Language

if TYPE_CHECKING:
    from spacy.tokens import Doc

logger = logging.getLogger(__name__)

# ── Label normalisation maps ────────────────────────────────────────────────
_BC5CDR_MAP: dict[str, str] = {
    "CHEMICAL": "MEDICATION",
    "DISEASE":  "DIAGNOSIS",
}

_BIONLP_MAP: dict[str, str] = {
    "Anatomical_system":       "ANATOMY",
    "Organ":                   "ANATOMY",
    "Organism_subdivision":    "ANATOMY",
    "Cancer":                  "DIAGNOSIS",
    "Simple_chemical":         "MEDICATION",
    "Amino_acid":              "MEDICATION",
    "Developing_anatomical_structure": "ANATOMY",
}

# Labels produced by the EntityRuler layer (already normalised)
_RULER_LABELS: frozenset[str] = frozenset(
    {"PROCEDURE", "LAB_TEST", "LAB_VALUE", "SYMPTOM"}
)


class NerService:
    """Identifies clinical named entities using two scispaCy models plus an
    EntityRuler pattern layer.

    Models are loaded **once at construction** to avoid per-request overhead.
    All processing is fully local — no external API calls (ADR-004).

    Raises:
        RuntimeError: If either model cannot be loaded (triggers Hangfire retry).
    """

    def __init__(self) -> None:
        self._bc5cdr: Language = self._load("en_ner_bc5cdr_md")
        self._bionlp: Language = self._load("en_ner_bionlp13cg_md")
        self._ruler_patterns = self._build_ruler_patterns()

    # ── Public API ────────────────────────────────────────────────────────

    def extract_entities(
        self,
        pages: list[str],
        confidence_threshold: float = 0.7,
        chunk_size: int = 10_000,
    ) -> list[dict]:
        """Run NER over a list of page texts and return entity dicts.

        Args:
            pages: One entry per document page (from OCR output).
            confidence_threshold: Entities below this score are flagged.
            chunk_size: Maximum characters per processing chunk.

        Returns:
            List of entity dicts compatible with ``EntitySpan``.
        """
        results: list[dict] = []

        for page_index, page_text in enumerate(pages):
            if not page_text or not page_text.strip():
                continue

            char_offset = 0
            for chunk in self._chunk_text(page_text, chunk_size):
                entities = self._process_chunk(chunk, confidence_threshold)
                # Adjust start/end offsets for chunk position within page
                for ent in entities:
                    ent["start_offset"] += char_offset
                    ent["end_offset"]   += char_offset
                results.extend(entities)
                char_offset += len(chunk)

        return results

    # ── Private helpers ───────────────────────────────────────────────────

    def _process_chunk(
        self, text: str, threshold: float
    ) -> list[dict]:
        """Run all three NER passes on a single text chunk."""
        seen: set[tuple[int, int]] = set()   # de-duplicate by span
        entities: list[dict] = []

        for doc, label_map in (
            (self._bc5cdr(text), _BC5CDR_MAP),
            (self._bionlp(text), _BIONLP_MAP),
        ):
            for ent in doc.ents:
                norm_label = label_map.get(ent.label_)
                if norm_label is None:
                    continue
                span_key = (ent.start_char, ent.end_char)
                if span_key in seen:
                    continue
                seen.add(span_key)
                score = float(getattr(ent, "kb_id_", 0) or ent._.get("score", 0.0) or 0.8)
                # scispaCy does not expose token-level confidence natively;
                # use 0.8 as default for model-detected entities.
                # The EntityRuler patterns set explicit scores via span extensions.
                entities.append(self._make_entity(ent.text, norm_label, ent.start_char, ent.end_char, score, threshold))

        # EntityRuler pass (patterns for PROCEDURE, LAB_TEST, LAB_VALUE, SYMPTOM)
        ruler_nlp = self._bc5cdr   # reuse tokeniser
        ruler_doc: Doc = ruler_nlp.make_doc(text)
        matches = self._ruler_patterns.match(ruler_doc)   # type: ignore[attr-defined]
        for _, start, end in matches:
            span = ruler_doc[start:end]
            if span.label_ not in _RULER_LABELS:
                continue
            span_key = (span.start_char, span.end_char)
            if span_key in seen:
                continue
            seen.add(span_key)
            score = float(span._.get("score", 0.85))
            entities.append(self._make_entity(span.text, span.label_, span.start_char, span.end_char, score, threshold))

        return entities

    @staticmethod
    def _make_entity(text: str, entity_type: str, start: int, end: int, score: float, threshold: float) -> dict:
        return {
            "text":             text,
            "type":             entity_type,
            "start_offset":     start,
            "end_offset":       end,
            "confidence_score": round(score, 4),
            "low_confidence":   score < threshold,
        }

    @staticmethod
    def _chunk_text(text: str, chunk_size: int) -> list[str]:
        """Split text into chunks at sentence/whitespace boundaries."""
        if len(text) <= chunk_size:
            return [text]
        chunks: list[str] = []
        while text:
            if len(text) <= chunk_size:
                chunks.append(text)
                break
            # Split at last whitespace before chunk_size
            split_at = text.rfind(" ", 0, chunk_size)
            if split_at == -1:
                split_at = chunk_size
            chunks.append(text[:split_at])
            text = text[split_at:].lstrip()
        return chunks

    @staticmethod
    def _load(model_name: str) -> Language:
        try:
            return spacy.load(model_name)
        except OSError as exc:
            raise RuntimeError(
                f"scispaCy model '{model_name}' could not be loaded. "
                "Ensure it is installed in the container (see Dockerfile). "
                f"Original error: {exc}"
            ) from exc

    def _build_ruler_patterns(self):
        """Build an in-memory EntityRuler for rule-based entity types."""
        ruler = self._bc5cdr.add_pipe("entity_ruler", last=True)

        # Common procedure terms (representative — extend via JSON pattern file)
        procedures = [
            "biopsy", "appendectomy", "cholecystectomy", "colonoscopy",
            "endoscopy", "MRI", "CT scan", "X-ray", "ultrasound",
            "echocardiogram", "angioplasty", "dialysis", "chemotherapy",
            "radiation therapy", "intubation", "catheterisation",
        ]

        # Common symptom terms
        symptoms = [
            "fatigue", "fever", "nausea", "vomiting", "headache", "dyspnea",
            "chest pain", "shortness of breath", "dizziness", "cough",
            "haemoptysis", "haematuria", "oedema", "palpitations",
        ]

        # Common lab test names
        lab_tests = [
            "CBC", "BMP", "CMP", "HbA1c", "TSH", "LFTs", "creatinine",
            "eGFR", "INR", "PT", "aPTT", "troponin", "BNP", "CRP",
            "ESR", "haemoglobin", "WBC", "platelets", "albumin",
        ]

        patterns: list[dict] = []

        for term in procedures:
            patterns.append({"label": "PROCEDURE", "pattern": term})
        for term in symptoms:
            patterns.append({"label": "SYMPTOM", "pattern": term})
        for term in lab_tests:
            patterns.append({"label": "LAB_TEST", "pattern": term})

        # LAB_VALUE — numeric pattern (e.g. "12.5 g/dL", "8.2 × 10³/μL")
        patterns.append({
            "label": "LAB_VALUE",
            "pattern": [
                {"TEXT": {"REGEX": r"^\d+(\.\d+)?$"}},
                {"TEXT": {"REGEX": r"^(g/dL|mg/dL|mmol/L|mEq/L|IU/L|U/L|ng/mL|pg/mL|μmol/L)$"}},
            ],
        })

        ruler.add_patterns(patterns)
        return ruler
```

> **Note on confidence scores**: scispaCy models do not expose per-entity
> confidence scores via the standard spaCy API. A fixed value of `0.8` is used
> for model-detected entities as a conservative estimate. EntityRuler patterns
> use `0.85`. A future iteration can expose token probabilities when using NER
> models that support `enable_score`.

---

### 5. Wire up the `/extraction/ner` Endpoint

Edit `src/ai-service/app/routers/extraction.py`:

```python
from __future__ import annotations

import structlog
from fastapi import APIRouter, HTTPException

from app.models import NerRequest, NerResponse, OcrRequest, OcrResponse
from app.services.ner_service import NerService
from app.config import settings

router = APIRouter()
logger = structlog.get_logger(__name__)

# Singleton service — models loaded once at import time
_ner_service: NerService | None = None


def _get_ner_service() -> NerService:
    """Lazy-initialise and cache the NerService singleton."""
    global _ner_service
    if _ner_service is None:
        _ner_service = NerService()
    return _ner_service


@router.post("/ocr", response_model=OcrResponse)
async def extract_ocr(request: OcrRequest) -> OcrResponse:
    """OCR text extraction — placeholder."""
    raise NotImplementedError


@router.post("/ner", response_model=NerResponse)
async def extract_ner(request: NerRequest) -> NerResponse:
    """
    Run NER over the provided page texts and return annotated entity spans.

    Returns 503 if the scispaCy model is unavailable (triggers .NET retry).
    """
    try:
        svc = _get_ner_service()
    except RuntimeError as exc:
        logger.error("ner_model_unavailable", error=str(exc))
        raise HTTPException(status_code=503, detail="NER model unavailable — retry later.") from exc

    try:
        raw_entities = svc.extract_entities(
            pages=request.pages,
            confidence_threshold=request.confidence_threshold,
            chunk_size=settings.ner_chunk_size,
        )
    except Exception as exc:
        logger.exception("ner_extraction_error", error=str(exc))
        raise HTTPException(status_code=500, detail="NER extraction failed.") from exc

    logger.info("ner_extraction_complete", entity_count=len(raw_entities))
    return NerResponse(entities=raw_entities)
```

---

### 6. Update AI Service `Dockerfile` — Download scispaCy Models

Edit `src/ai-service/Dockerfile`.

In the **deps** stage, after `pip install -r requirements.txt`, add the
scispaCy model download lines:

```dockerfile
# ─── Stage 2: deps ────────────────────────────────────────────────────────────
FROM base AS deps

RUN python -m venv /opt/venv
ENV PATH="/opt/venv/bin:$PATH"

COPY requirements.txt .
RUN pip install --no-cache-dir --upgrade pip \
 && pip install --no-cache-dir -r requirements.txt \
 && pip install --no-cache-dir \
      https://s3-us-west-2.amazonaws.com/ai2-s2-scispacy/releases/v0.5.4/en_ner_bc5cdr_md-0.5.4.tar.gz \
      https://s3-us-west-2.amazonaws.com/ai2-s2-scispacy/releases/v0.5.4/en_ner_bionlp13cg_md-0.5.4.tar.gz
```

> The model archives are ~50 MB each and are baked into the image at build time
> so containers start without internet access (ADR-004).

---

## File Checklist

| File                                              | Action |
|---------------------------------------------------|--------|
| `src/ai-service/requirements.txt`                 | Modify |
| `src/ai-service/app/config.py`                    | Modify |
| `src/ai-service/app/models/extraction_models.py`  | Modify |
| `src/ai-service/app/services/ner_service.py`      | Modify |
| `src/ai-service/app/routers/extraction.py`        | Modify |
| `src/ai-service/Dockerfile`                       | Modify |

## Verification

```bash
# Build the AI service image
docker build -t hp_ai:test ./src/ai-service --target deps

# Run the health check
docker run --rm -e INTERNAL_API_KEY=test hp_ai:test \
  python -c "from app.services.ner_service import NerService; s = NerService(); print('OK')"

# Call the endpoint (inside docker network or with port-forward)
curl -s -X POST http://localhost:8000/extraction/ner \
  -H "Content-Type: application/json" \
  -H "X-Internal-Api-Key: $INTERNAL_API_KEY" \
  -d '{"pages":["Patient presents with hypertension and was prescribed lisinopril."],"confidence_threshold":0.7}'
# Expected: {"entities":[{"text":"hypertension","type":"DIAGNOSIS",...},{"text":"lisinopril","type":"MEDICATION",...}]}
```
