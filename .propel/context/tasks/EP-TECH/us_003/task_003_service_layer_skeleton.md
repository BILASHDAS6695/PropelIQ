# Task 003: Service Layer Skeleton

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-003 |
| **Epic** | EP-TECH |
| **Layer** | Python / Service |
| **Priority** | Critical |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 001 |

## Objective

Implement placeholder service classes for OCR, NER, coding, and intake NLP processing. Each class defines the public interface that the corresponding router will call, raising `NotImplementedError` to signal that real logic is pending future user stories.

## Implementation Steps

### 1. Create the OCR Service

**File:** `src/ai-service/app/services/ocr_service.py`

```python
"""OCR extraction service — local processing only (ADR-004)."""

from __future__ import annotations


class OcrService:
    """Extracts raw text from document images or PDF pages using Tesseract/PyMuPDF.

    No external API calls are made. All processing is local per ADR-004.
    """

    def extract_text_from_image(self, image_bytes: bytes) -> str:
        """Extract text from a raw image byte buffer.

        Args:
            image_bytes: Raw image data (PNG, JPEG, TIFF).

        Returns:
            Extracted text string.

        Raises:
            NotImplementedError: Until OCR logic is implemented.
        """
        raise NotImplementedError("OCR extraction not yet implemented")

    def extract_text_from_pdf(self, pdf_bytes: bytes) -> list[str]:
        """Extract per-page text from a PDF byte buffer using PyMuPDF.

        Args:
            pdf_bytes: Raw PDF data.

        Returns:
            List of text strings, one entry per page.

        Raises:
            NotImplementedError: Until PDF extraction logic is implemented.
        """
        raise NotImplementedError("PDF text extraction not yet implemented")
```

### 2. Create the NER Service

**File:** `src/ai-service/app/services/ner_service.py`

```python
"""Named Entity Recognition service — local spaCy processing (ADR-004)."""

from __future__ import annotations


class NerService:
    """Identifies clinical named entities (conditions, medications, procedures)
    from plain text using a local spaCy model.

    No external API calls are made. All processing is local per ADR-004.
    """

    def extract_entities(self, text: str) -> list[dict[str, str]]:
        """Run NER over the provided text and return entity spans.

        Args:
            text: Plain text input from OCR or direct submission.

        Returns:
            List of entity dicts with keys: ``label``, ``text``, ``start``, ``end``.

        Raises:
            NotImplementedError: Until spaCy model is loaded and wired.
        """
        raise NotImplementedError("NER entity extraction not yet implemented")
```

### 3. Create the Coding Service

**File:** `src/ai-service/app/services/coding_service.py`

```python
"""Medical coding service — ICD/CPT suggestion from clinical text (ADR-004)."""

from __future__ import annotations


class CodingService:
    """Suggests ICD-10-CM and CPT codes from clinical text or NER output.

    No external API calls are made. All processing is local per ADR-004.
    """

    def suggest_codes(self, clinical_text: str) -> list[dict[str, str]]:
        """Suggest ICD/CPT codes for the given clinical text.

        Args:
            clinical_text: Free-text clinical narrative.

        Returns:
            List of code suggestion dicts with keys: ``code``, ``description``, ``confidence``.

        Raises:
            NotImplementedError: Until coding model is implemented.
        """
        raise NotImplementedError("Code suggestion not yet implemented")

    def validate_codes(self, codes: list[str]) -> dict[str, bool]:
        """Validate a list of codes against the known code sets.

        Args:
            codes: List of ICD/CPT code strings.

        Returns:
            Dict mapping each code to its validity (True/False).

        Raises:
            NotImplementedError: Until code validation is implemented.
        """
        raise NotImplementedError("Code validation not yet implemented")
```

### 4. Create the Intake NLP Service

**File:** `src/ai-service/app/services/intake_service.py`

```python
"""Intake NLP service — form text parsing and classification (ADR-004)."""

from __future__ import annotations


class IntakeService:
    """Parses and classifies patient intake form text using local NLP models.

    No external API calls are made. All processing is local per ADR-004.
    """

    def parse_intake_form(self, form_text: str) -> dict[str, str | list[str]]:
        """Parse structured fields from free-text intake form content.

        Args:
            form_text: Raw text from a patient intake form.

        Returns:
            Dict of parsed fields (e.g., chief_complaint, medications, allergies).

        Raises:
            NotImplementedError: Until form parsing logic is implemented.
        """
        raise NotImplementedError("Intake form parsing not yet implemented")

    def classify_intake(self, form_text: str) -> str:
        """Classify intake submission into a processing category.

        Args:
            form_text: Raw text from a patient intake form.

        Returns:
            Classification label string (e.g., "routine", "urgent", "emergency").

        Raises:
            NotImplementedError: Until classification model is implemented.
        """
        raise NotImplementedError("Intake classification not yet implemented")
```

### 5. Register Services for Dependency Injection

Add a module-level factory to each service's `__init__.py` so routers can access them via FastAPI's `Depends()` when wiring is done in later user stories.

**File:** `src/ai-service/app/services/__init__.py`

```python
from app.services.ocr_service import OcrService
from app.services.ner_service import NerService
from app.services.coding_service import CodingService
from app.services.intake_service import IntakeService

__all__ = ["OcrService", "NerService", "CodingService", "IntakeService"]
```

## Acceptance Criteria

- [ ] `OcrService` class exists with `extract_text_from_image()` and `extract_text_from_pdf()` methods
- [ ] `NerService` class exists with `extract_entities()` method
- [ ] `CodingService` class exists with `suggest_codes()` and `validate_codes()` methods
- [ ] `IntakeService` class exists with `parse_intake_form()` and `classify_intake()` methods
- [ ] All methods raise `NotImplementedError` (not `pass`) as their placeholder body
- [ ] All service classes are importable from `app.services`
- [ ] No external AI API calls are present in any service file

## Verification

```bash
cd src/ai-service
python -c "
from app.services import OcrService, NerService, CodingService, IntakeService
print('OcrService:', OcrService.__name__)
print('NerService:', NerService.__name__)
print('CodingService:', CodingService.__name__)
print('IntakeService:', IntakeService.__name__)
print('All service classes importable.')
"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-003 AC-4 | Service layer skeleton with placeholder classes for OCR, NER, coding, intake NLP |
| ADR-004 | Local AI Inference — no external API calls |
| AIR-025 | Data privacy — no external AI transmission |
