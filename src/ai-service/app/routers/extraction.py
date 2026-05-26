from fastapi import APIRouter

from app.models import NerRequest, NerResponse, OcrRequest, OcrResponse

router = APIRouter()


@router.post("/ocr", response_model=OcrResponse)
async def extract_ocr(request: OcrRequest) -> OcrResponse:
    """OCR text extraction — placeholder."""
    raise NotImplementedError


@router.post("/ner", response_model=NerResponse)
async def extract_ner(request: NerRequest) -> NerResponse:
    """NER extraction — placeholder."""
    raise NotImplementedError
