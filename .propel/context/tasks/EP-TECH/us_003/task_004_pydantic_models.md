# Task 004: Pydantic v2 Request & Response Models

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-003 |
| **Epic** | EP-TECH |
| **Layer** | Python / Models |
| **Priority** | Critical |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001, Task 002 |

## Objective

Define all Pydantic v2 request and response models for the extraction, coding, and intake processing domains. Models establish the API contract between the .NET backend caller and the AI sidecar before any processing logic is implemented.

## Implementation Steps

### 1. Create Extraction Models

**File:** `src/ai-service/app/models/extraction_models.py`

```python
"""Pydantic v2 models for the /extraction domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class DocumentFormat(StrEnum):
    PDF = "pdf"
    PNG = "png"
    JPEG = "jpeg"
    TIFF = "tiff"


# --- OCR ---

class OcrRequest(BaseModel):
    """Request body for POST /extraction/ocr."""

    document_base64: str = Field(
        ...,
        description="Base64-encoded document bytes (image or PDF).",
        min_length=1,
    )
    format: DocumentFormat = Field(
        default=DocumentFormat.PDF,
        description="File format of the encoded document.",
    )
    page_numbers: list[int] | None = Field(
        default=None,
        description="Optional list of 1-based page numbers to extract (PDF only). "
                    "None means all pages.",
    )


class OcrResponse(BaseModel):
    """Response body for POST /extraction/ocr."""

    pages: list[str] = Field(description="Extracted text per page.")
    total_pages: int = Field(description="Total number of pages processed.")


# --- NER ---

class NerRequest(BaseModel):
    """Request body for POST /extraction/ner."""

    text: str = Field(..., description="Plain text to run NER over.", min_length=1)


class EntitySpan(BaseModel):
    label: str = Field(description="Entity label (e.g., CONDITION, MEDICATION).")
    text: str = Field(description="Surface text of the entity.")
    start: int = Field(description="Character start offset.")
    end: int = Field(description="Character end offset.")


class NerResponse(BaseModel):
    """Response body for POST /extraction/ner."""

    entities: list[EntitySpan] = Field(description="Detected entity spans.")
```

### 2. Create Coding Models

**File:** `src/ai-service/app/models/coding_models.py`

```python
"""Pydantic v2 models for the /coding domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class CodeSystem(StrEnum):
    ICD10CM = "ICD-10-CM"
    CPT = "CPT"


# --- Suggest ---

class CodeSuggestRequest(BaseModel):
    """Request body for POST /coding/suggest."""

    clinical_text: str = Field(
        ...,
        description="Free-text clinical narrative to generate code suggestions for.",
        min_length=1,
    )
    code_systems: list[CodeSystem] = Field(
        default=[CodeSystem.ICD10CM],
        description="Which code systems to query.",
    )
    max_suggestions: int = Field(
        default=10,
        ge=1,
        le=50,
        description="Maximum number of suggestions to return per code system.",
    )


class CodeSuggestion(BaseModel):
    code: str = Field(description="Suggested code (e.g., Z00.00).")
    description: str = Field(description="Human-readable description of the code.")
    code_system: CodeSystem
    confidence: float = Field(ge=0.0, le=1.0, description="Model confidence score.")


class CodeSuggestResponse(BaseModel):
    """Response body for POST /coding/suggest."""

    suggestions: list[CodeSuggestion]


# --- Validate ---

class CodeValidateRequest(BaseModel):
    """Request body for POST /coding/validate."""

    codes: list[str] = Field(
        ...,
        description="List of codes to validate.",
        min_length=1,
    )
    code_system: CodeSystem = Field(
        default=CodeSystem.ICD10CM,
        description="Code system context for validation.",
    )


class CodeValidateResponse(BaseModel):
    """Response body for POST /coding/validate."""

    results: dict[str, bool] = Field(
        description="Map of code → validity (True = valid, False = invalid)."
    )
```

### 3. Create Intake Models

**File:** `src/ai-service/app/models/intake_models.py`

```python
"""Pydantic v2 models for the /intake domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class IntakeCategory(StrEnum):
    ROUTINE = "routine"
    URGENT = "urgent"
    EMERGENCY = "emergency"
    UNKNOWN = "unknown"


# --- Parse ---

class IntakeParseRequest(BaseModel):
    """Request body for POST /intake/parse."""

    form_text: str = Field(
        ...,
        description="Raw text captured from the patient intake form.",
        min_length=1,
    )


class IntakeParseResponse(BaseModel):
    """Response body for POST /intake/parse."""

    chief_complaint: str | None = Field(default=None)
    symptoms: list[str] = Field(default_factory=list)
    medications: list[str] = Field(default_factory=list)
    allergies: list[str] = Field(default_factory=list)
    medical_history: list[str] = Field(default_factory=list)
    raw_fields: dict[str, str] = Field(
        default_factory=dict,
        description="Any additional key-value pairs extracted from the form.",
    )


# --- Classify ---

class IntakeClassifyRequest(BaseModel):
    """Request body for POST /intake/classify."""

    form_text: str = Field(
        ...,
        description="Raw text captured from the patient intake form.",
        min_length=1,
    )


class IntakeClassifyResponse(BaseModel):
    """Response body for POST /intake/classify."""

    category: IntakeCategory
    confidence: float = Field(ge=0.0, le=1.0)
```

### 4. Export All Models from `models/__init__.py`

**File:** `src/ai-service/app/models/__init__.py`

```python
from app.models.extraction_models import (
    DocumentFormat,
    OcrRequest,
    OcrResponse,
    NerRequest,
    NerResponse,
    EntitySpan,
)
from app.models.coding_models import (
    CodeSystem,
    CodeSuggestRequest,
    CodeSuggestResponse,
    CodeSuggestion,
    CodeValidateRequest,
    CodeValidateResponse,
)
from app.models.intake_models import (
    IntakeCategory,
    IntakeParseRequest,
    IntakeParseResponse,
    IntakeClassifyRequest,
    IntakeClassifyResponse,
)

__all__ = [
    "DocumentFormat",
    "OcrRequest", "OcrResponse",
    "NerRequest", "NerResponse", "EntitySpan",
    "CodeSystem",
    "CodeSuggestRequest", "CodeSuggestResponse", "CodeSuggestion",
    "CodeValidateRequest", "CodeValidateResponse",
    "IntakeCategory",
    "IntakeParseRequest", "IntakeParseResponse",
    "IntakeClassifyRequest", "IntakeClassifyResponse",
]
```

### 5. Wire Models into Router Stubs

Update the router stubs from Task 002 to accept and return typed models. Example for extraction:

**File:** `src/ai-service/app/routers/extraction.py` (update)

```python
from fastapi import APIRouter
from app.models import OcrRequest, OcrResponse, NerRequest, NerResponse

router = APIRouter()


@router.post("/ocr", response_model=OcrResponse)
async def extract_ocr(request: OcrRequest) -> OcrResponse:
    """OCR text extraction — placeholder."""
    raise NotImplementedError


@router.post("/ner", response_model=NerResponse)
async def extract_ner(request: NerRequest) -> NerResponse:
    """NER extraction — placeholder."""
    raise NotImplementedError
```

Apply the same pattern to `coding.py` and `intake.py`.

## Acceptance Criteria

- [ ] `OcrRequest` and `OcrResponse` Pydantic v2 models exist in `extraction_models.py`
- [ ] `NerRequest`, `NerResponse`, `EntitySpan` models exist
- [ ] `CodeSuggestRequest`, `CodeSuggestResponse`, `CodeValidateRequest`, `CodeValidateResponse` models exist
- [ ] `IntakeParseRequest`, `IntakeParseResponse`, `IntakeClassifyRequest`, `IntakeClassifyResponse` models exist
- [ ] All models use Pydantic `v2` (`model_config`, `Field()`, no `class Config`)
- [ ] All models are importable from `app.models`
- [ ] Router stubs reference typed request/response models

## Verification

```bash
cd src/ai-service
python -c "
from app.models import (
    OcrRequest, OcrResponse,
    NerRequest, NerResponse,
    CodeSuggestRequest, CodeSuggestResponse,
    IntakeParseRequest, IntakeParseResponse,
)
# Instantiate sample models to verify schema generation
sample = OcrRequest(document_base64='dGVzdA==', format='pdf')
print('OcrRequest schema OK:', sample.model_dump())
print('All Pydantic v2 models importable.')
"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-003 AC-9 | Basic request/response models defined with Pydantic |
| US-003 (Technical Notes) | Pydantic v2 for models |
| ADR-003 | Python Sidecar for AI |
