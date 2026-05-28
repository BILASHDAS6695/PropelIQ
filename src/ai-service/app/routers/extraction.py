from __future__ import annotations

import structlog
from fastapi import APIRouter, HTTPException

from app.config import settings
from app.models import EntitySpan, NerRequest, NerResponse, OcrRequest, OcrResponse
from app.services.ner_service import NerService

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
    return NerResponse(entities=[EntitySpan(**e) for e in raw_entities])

